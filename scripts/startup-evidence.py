"""Loopback-only startup probe, with synthetic credentials and no payment requests."""
import json, os, pathlib, subprocess, sys, time, urllib.request
output = pathlib.Path(sys.argv[2]).resolve()
output.mkdir(parents=True, exist_ok=True)
env = os.environ.copy()
# Remove inherited external integrations; all configured probe targets are loopback.
for key in list(env):
    if any(s in key.lower() for s in ('paypal', 'paymongo', 'stripe', 'supabase', 'database', 'jwt', 'connectionstrings')):
        del env[key]
env.update({
    'ASPNETCORE_ENVIRONMENT': 'Development', 'ASPNETCORE_URLS': 'http://127.0.0.1:5084',
    'PORT': '', 'Database__AllowLocalPlaintext': 'true', 'SUPABASE_URL': 'http://127.0.0.1:59999',
    'SUPABASE_SERVICE_ROLE_KEY': 'synthetic-nonproduction-key',
    'Supabase__Url': 'http://127.0.0.1:59999', 'Supabase__ServiceRoleKey': 'synthetic-nonproduction-key',
    'Jwt__Key': 'synthetic-ci-only-signing-key-never-for-deployment-20261012',
    'DATABASE_URL': 'postgresql://alpha_ci:disposable-ci-only@127.0.0.1:5432/alpha_model_ci',
})
with (output / 'startup.log').open('w') as log:
    process = subprocess.Popen([sys.argv[1], sys.argv[3]], env=env, stdout=log, stderr=subprocess.STDOUT)
    try:
        opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
        for attempt in range(60):
            if process.poll() is not None:
                raise RuntimeError('Application exited before health response')
            try:
                with opener.open('http://127.0.0.1:5084/health', timeout=1) as response:
                    body = response.read().decode()
                    assert response.status == 200 and body == 'API WORKING'
                    result = {'startup': 'PASS', 'healthStatus': 200, 'healthBody': body,
                              'scope': 'Development; synthetic keys; loopback targets; health only',
                              'databaseReadinessFromHealth': False, 'supabaseIntegrationVerified': False}
                    (output / 'startup-result.json').write_text(json.dumps(result, indent=2))
                    print(json.dumps(result))
                    break
            except (OSError, urllib.error.URLError):
                time.sleep(0.5)
        else:
            raise RuntimeError('Health timeout')
    finally:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
