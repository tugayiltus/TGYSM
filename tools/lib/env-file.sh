# Reads values from a docker compose env file (deploy/.env) without executing it. Source this file, then:
#   env_value <file> <key>   prints the last assignment of <key>, or nothing when the file or key is missing.
# Tolerates CRLF line ends, spaces around the value, an "export " prefix, matching single/double quotes and an inline
# " #" comment after an unquoted value.
env_value() {
  local file="$1" key="$2" line value=""
  [[ -f "$file" ]] || return 0
  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%$'\r'}"
    line="${line#"${line%%[![:space:]]*}"}"
    line="${line#export }"
    [[ "$line" == "$key="* ]] || continue
    value="${line#"$key="}"
    value="${value#"${value%%[![:space:]]*}"}"
    if [[ "$value" =~ ^\"(.*)\"[[:space:]]*(#.*)?$ || "$value" =~ ^\'(.*)\'[[:space:]]*(#.*)?$ ]]; then
      value="${BASH_REMATCH[1]}"
    else
      value="${value%%[[:space:]]#*}"
      value="${value%"${value##*[![:space:]]}"}"
    fi
  done < "$file"
  printf '%s' "$value"
}
