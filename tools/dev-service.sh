#!/usr/bin/env bash
# Keeps the local dev stack (API on :5080, Worker, Vite on :5173) running as macOS launchd user agents: each starts
# at login and is restarted whenever its process exits. Logs go to tmp/logs/.
#
# Usage: tools/dev-service.sh install|uninstall|start|stop|restart|status|logs
#   install    write the agents and start them (run again after moving the repo)
#   uninstall  stop the agents and remove them
#   start      start the agents (after stop)
#   stop       stop the agents until the next start or login (e.g. to free ports for tools/e2e.sh)
#   restart    restart all three, e.g. to pick up backend code changes
#   status     launchd state, PIDs and HTTP checks
#   logs       last lines of every service log
set -euo pipefail
cd "$(dirname "$0")/.."

readonly ROOT="$(pwd)"
readonly LABEL_PREFIX="com.tgysm.dev"
readonly SERVICES=(api worker web)
readonly AGENTS_DIR="$HOME/Library/LaunchAgents"
readonly LOG_DIR="$ROOT/tmp/logs"
readonly DOMAIN="gui/$(id -u)"
# Seconds launchd waits before restarting a service that exited, so a crash loop does not spin the CPU.
readonly RESTART_THROTTLE_SECONDS=10
readonly LOG_TAIL_LINES=30
readonly API_HEALTH_URL="http://localhost:5080/api/v1/health"
readonly WEB_URL="http://localhost:5173"
readonly HTTP_TIMEOUT_SECONDS=3

# launchd starts agents with a minimal PATH; give them the toolchain this shell uses.
service_path() {
  local dotnet_dir npm_dir psql_dir
  dotnet_dir="$(dirname "$(command -v dotnet)")"
  npm_dir="$(dirname "$(command -v npm)")"
  psql_dir="$(dirname "$(command -v psql)")"
  echo "$dotnet_dir:$npm_dir:$psql_dir:/usr/bin:/bin:/usr/sbin:/sbin"
}

label() { echo "$LABEL_PREFIX.$1"; }
plist() { echo "$AGENTS_DIR/$(label "$1").plist"; }
loaded() { launchctl print "$DOMAIN/$(label "$1")" >/dev/null 2>&1; }

write_plist() {
  local service="$1" path
  path="$(service_path)"
  cat >"$(plist "$service")" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key><string>$(label "$service")</string>
  <key>ProgramArguments</key>
  <array><string>/bin/bash</string><string>$ROOT/tools/dev-$service.sh</string></array>
  <key>WorkingDirectory</key><string>$ROOT</string>
  <key>EnvironmentVariables</key>
  <dict>
    <key>PATH</key><string>$path</string>
    <key>HOME</key><string>$HOME</string>
  </dict>
  <key>RunAtLoad</key><true/>
  <key>KeepAlive</key><true/>
  <key>ThrottleInterval</key><integer>$RESTART_THROTTLE_SECONDS</integer>
  <key>StandardOutPath</key><string>$LOG_DIR/$service.log</string>
  <key>StandardErrorPath</key><string>$LOG_DIR/$service.log</string>
</dict>
</plist>
EOF
}

start_all() {
  for service in "${SERVICES[@]}"; do
    if loaded "$service"; then
      launchctl kickstart "$DOMAIN/$(label "$service")"
    else
      launchctl bootstrap "$DOMAIN" "$(plist "$service")"
    fi
  done
}

stop_all() {
  for service in "${SERVICES[@]}"; do
    if loaded "$service"; then launchctl bootout "$DOMAIN/$(label "$service")"; fi
  done
}

http_check() {
  if curl -fsS --max-time "$HTTP_TIMEOUT_SECONDS" -o /dev/null "$2"; then echo "$1: up ($2)"; else echo "$1: down ($2)"; fi
}

case "${1:-}" in
  install)
    mkdir -p "$AGENTS_DIR" "$LOG_DIR"
    stop_all
    for service in "${SERVICES[@]}"; do write_plist "$service"; done
    start_all
    echo "Installed. Services start at login and restart on exit. Check: tools/dev-service.sh status"
    ;;
  uninstall)
    stop_all
    for service in "${SERVICES[@]}"; do rm -f "$(plist "$service")"; done
    echo "Uninstalled."
    ;;
  start) start_all ;;
  stop) stop_all ;;
  restart)
    for service in "${SERVICES[@]}"; do launchctl kickstart -k "$DOMAIN/$(label "$service")"; done
    ;;
  status)
    for service in "${SERVICES[@]}"; do
      if loaded "$service"; then
        state="$(launchctl print "$DOMAIN/$(label "$service")" | awk -F'= ' '/^\tstate/ {print $2; exit}')"
        pid="$(launchctl print "$DOMAIN/$(label "$service")" | awk -F'= ' '/^\tpid/ {print $2; exit}')"
        echo "$service: ${state:-unknown} (pid ${pid:--})"
      else
        echo "$service: not loaded"
      fi
    done
    http_check api "$API_HEALTH_URL"
    http_check web "$WEB_URL"
    ;;
  logs)
    for service in "${SERVICES[@]}"; do
      echo "== $service"
      tail -n "$LOG_TAIL_LINES" "$LOG_DIR/$service.log" 2>/dev/null || echo "(no log yet)"
    done
    ;;
  *)
    sed -n '2,13p' "$0" >&2
    exit 2
    ;;
esac
