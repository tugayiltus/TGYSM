"""HTTP client for tools/restore-drill.sh (ADR-0025). Python standard library only.

seed  <base-url> <state.json> <admin-email> <admin-temporary-password>
      Creates data on the source stack: an admin password, an intake team, an employee whose incident carries an
      attachment, a ready background export and a deactivated user. Saves what the checks need, including the
      employee's session cookies.
check <base-url> <state.json>
      Runs the checks against a restored stack and prints one JSON line per check.
"""

import http.cookiejar
import json
import ssl
import sys
import time
import urllib.error
import urllib.request
import uuid

API = "/api/v1"
CSRF_HEADER = "X-CSRF-TOKEN"
ADMIN_PASSWORD = "Drill-admin-pass-2026"
USER_PASSWORD = "Drill-user-pass-2026"
ATTACHMENT_NAME = "drill.txt"
ATTACHMENT_BYTES = ("TGYSM restore drill attachment " * 64).encode()
EXPORT_REPORT = "incidents"
EXPORT_METRIC = "open_backlog"
EXPORT_WAIT_SECONDS = 180
EXPORT_POLL_SECONDS = 3
HTTP_TIMEOUT_SECONDS = 30
TEMPORARY_PASSWORD = "temporary_password"


class Client:
    def __init__(self, base, jar=None):
        self.base = base.rstrip("/") + API
        self.jar = jar if jar is not None else http.cookiejar.CookieJar()
        context = ssl.create_default_context()
        # Drill stacks use a self-signed certificate.
        context.check_hostname = False
        context.verify_mode = ssl.CERT_NONE
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(self.jar), urllib.request.HTTPSHandler(context=context))

    def request(self, method, path, body=None, raw=None, content_type="application/json"):
        headers = {}
        if method != "GET":
            headers[CSRF_HEADER] = self.csrf()
        data = raw
        if body is not None:
            data = json.dumps(body).encode()
        if data is not None:
            headers["Content-Type"] = content_type
        req = urllib.request.Request(self.base + path, data=data, headers=headers, method=method)
        try:
            with self.opener.open(req, timeout=HTTP_TIMEOUT_SECONDS) as resp:
                return resp.status, resp.read()
        except urllib.error.HTTPError as error:
            return error.code, error.read()

    def csrf(self):
        req = urllib.request.Request(self.base + "/auth/csrf")
        with self.opener.open(req, timeout=HTTP_TIMEOUT_SECONDS) as resp:
            return json.loads(resp.read())["token"]

    def json(self, method, path, body=None, expect=(200, 201, 202)):
        status, payload = self.request(method, path, body)
        if status not in expect:
            raise SystemExit(f"{method} {path} -> {status}: {payload[:300]!r}")
        return json.loads(payload) if payload else None

    def sign_in(self, email, password, new_password=None):
        result = self.json("POST", "/auth/login", {"email": email, "password": password})
        if result.get("mustChangePassword") and new_password:
            self.json("POST", "/auth/change-password", {"currentPassword": password, "newPassword": new_password}, expect=(200, 204))
            return new_password
        return password


def multipart(fields, files):
    boundary = uuid.uuid4().hex
    parts = []
    for name, value in fields.items():
        parts.append(f"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"\r\n\r\n{value}\r\n".encode())
    for name, (filename, content, ctype) in files.items():
        parts.append(
            f"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"; filename=\"{filename}\"\r\n"
            f"Content-Type: {ctype}\r\n\r\n".encode() + content + b"\r\n")
    parts.append(f"--{boundary}--\r\n".encode())
    return b"".join(parts), f"multipart/form-data; boundary={boundary}"


def seed(base, state_path, admin_email, admin_temporary):
    suffix = uuid.uuid4().hex[:8]
    admin = Client(base)
    admin.sign_in(admin_email, admin_temporary, ADMIN_PASSWORD)
    me = admin.json("GET", "/auth/me")
    location = admin.json("POST", "/admin/locations", {"name": f"Drill {suffix}", "parentId": None})
    department = admin.json("POST", "/admin/departments", {"name": f"Drill {suffix}", "approverUserId": me["id"]})
    team = admin.json("POST", "/admin/teams", {"name": f"Drill {suffix}"})
    admin.json("POST", f"/admin/teams/{team['id']}/members", {"userId": me["id"]})
    admin.json("PUT", f"/admin/teams/{team['id']}", {"version": team["version"], "name": team["name"], "isDefaultIntake": True})

    def new_user(name):
        email = f"{name}-{suffix}@example.com"
        created = admin.json("POST", "/admin/users", {
            "firstName": "Drill", "lastName": name.capitalize(), "email": email, "role": "employee",
            "departmentId": department["id"], "locationId": location["id"], "delivery": TEMPORARY_PASSWORD})
        return email, created["userId"], created["temporaryPassword"]

    employee_email, _, employee_temp = new_user("employee")
    gone_email, gone_id, gone_temp = new_user("gone")
    gone = Client(base)
    gone_password = gone.sign_in(gone_email, gone_temp, USER_PASSWORD)
    gone_user = admin.json("GET", f"/admin/users/{gone_id}")
    admin.json("POST", f"/admin/users/{gone_id}/deactivate", {"version": gone_user["version"]}, expect=(200, 204))

    employee = Client(base)
    employee.sign_in(employee_email, employee_temp, USER_PASSWORD)
    ticket = employee.json("POST", "/tickets/incidents", {
        "subject": f"Drill {suffix}", "description": "Restore drill", "categoryId": None, "locationId": None, "assetId": None})
    body, ctype = multipart({"body": "Ek", "visibility": "public"}, {"files": (ATTACHMENT_NAME, ATTACHMENT_BYTES, "text/plain")})
    status, payload = employee.request("POST", f"/tickets/{ticket['number']}/comments", raw=body, content_type=ctype)
    if status != 201:
        raise SystemExit(f"comment upload -> {status}: {payload[:300]!r}")
    comments = employee.json("GET", f"/tickets/{ticket['number']}/comments")
    attachment = next(a for c in comments for a in c["attachments"])

    admin.json("POST", f"/reports/exports?report={EXPORT_REPORT}&metric={EXPORT_METRIC}", expect=(202,))
    deadline = time.time() + EXPORT_WAIT_SECONDS
    export = None
    while time.time() < deadline and export is None:
        jobs = admin.json("GET", "/reports/exports")["items"]
        export = next((j for j in jobs if j["status"] == "ready"), None)
        if export is None:
            time.sleep(EXPORT_POLL_SECONDS)
    if export is None:
        raise SystemExit("background export did not become ready")
    _, export_bytes = admin.request("GET", f"/reports/exports/{export['id']}/download")

    jar_cookies = [{"name": c.name, "value": c.value, "domain": c.domain, "path": c.path, "secure": c.secure} for c in employee.jar]
    admin_cookies = [{"name": c.name, "value": c.value, "domain": c.domain, "path": c.path, "secure": c.secure} for c in admin.jar]
    state = {
        "attachmentPath": f"/tickets/{ticket['number']}/attachments/{attachment['id']}",
        "attachmentSize": len(ATTACHMENT_BYTES),
        "exportPath": f"/reports/exports/{export['id']}/download",
        "exportSize": len(export_bytes),
        "employeeCookies": jar_cookies,
        "adminCookies": admin_cookies,
        "goneEmail": gone_email,
        "gonePassword": gone_password,
    }
    with open(state_path, "w", encoding="utf-8") as handle:
        json.dump(state, handle)
    print(f"seeded ticket {ticket['number']}, attachment {state['attachmentSize']} B, export {state['exportSize']} B")


def jar_from(cookies):
    jar = http.cookiejar.CookieJar()
    for c in cookies:
        jar.set_cookie(http.cookiejar.Cookie(
            0, c["name"], c["value"], None, False, c["domain"], bool(c["domain"]), c["domain"].startswith("."),
            c["path"], True, c["secure"], None, False, None, None, {}))
    return jar


def check(base, state_path):
    with open(state_path, encoding="utf-8") as handle:
        state = json.load(handle)
    results = []

    def record(name, expected, actual):
        results.append({"check": name, "expected": expected, "actual": actual, "ok": expected == actual})

    status, _ = Client(base).request("GET", "/health")
    record("health", 200, status)
    employee = Client(base, jar_from(state["employeeCookies"]))
    status, _ = employee.request("GET", "/auth/me")
    record("session from before the backup is still valid", 200, status)
    status, body = employee.request("GET", state["attachmentPath"])
    record("attachment downloads with the same size", state["attachmentSize"], len(body) if status == 200 else f"HTTP {status}")
    admin = Client(base, jar_from(state["adminCookies"]))
    status, body = admin.request("GET", state["exportPath"])
    record("export file downloads with the same size", state["exportSize"], len(body) if status == 200 else f"HTTP {status}")
    status, _ = Client(base).request("POST", "/auth/login", {"email": state["goneEmail"], "password": state["gonePassword"]})
    record("deactivated user cannot sign in", "refused", "refused" if status >= 400 else f"HTTP {status}")

    for result in results:
        print(json.dumps(result, ensure_ascii=False))
    return 0 if all(r["ok"] for r in results) else 1


if __name__ == "__main__":
    command = sys.argv[1] if len(sys.argv) > 1 else ""
    if command == "seed" and len(sys.argv) == 6:
        seed(*sys.argv[2:6])
    elif command == "check" and len(sys.argv) == 4:
        sys.exit(check(sys.argv[2], sys.argv[3]))
    else:
        print(__doc__, file=sys.stderr)
        sys.exit(2)
