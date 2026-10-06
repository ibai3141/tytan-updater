"""Exercise actual PHP endpoints with temporary fixtures and optional C# integration."""

import argparse
import base64
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
import zipfile


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--php', default='php', help='Path to php.exe or the PHP executable')
    parser.add_argument('--with-client', action='store_true', help='Also run the C# tests against real PHP')
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[2]
    php = shutil.which(args.php) or args.php

    for source in sorted((repo / 'server').glob('*.php')):
        subprocess.run([php, '-l', str(source)], check=True)

    with tempfile.TemporaryDirectory(prefix='tytan-php-tests-') as temporary:
        workspace = Path(temporary)
        root = workspace / 'SQLupdate'
        root.mkdir()
        for source in (repo / 'server').glob('*.php'):
            shutil.copy2(source, root / source.name)
        client = root / 'cliente'
        client.mkdir()
        (root / 'Empty').mkdir()
        spaced = root / 'Client & more'
        spaced.mkdir()
        for directory, name in [(client, 'Faktury_008.000.043.zip'), (client, 'Faktury_008.000.040.zip'),
                                (client, 'FK2025_999.000.000.zip'), (spaced, 'Faktury_008.000.043.zip')]:
            with zipfile.ZipFile(directory / name, 'w') as archive:
                archive.writestr('example.txt', 'PHP integration sample')
        (client / 'notes.txt').write_text('private notes', encoding='utf-8')
        (client / '.hidden.zip').write_bytes(b'hidden')
        (root / '.htaccess').write_text('private config', encoding='utf-8')
        sibling = workspace / 'SQLupdate-other'
        sibling.mkdir()
        (sibling / 'secret.zip').write_bytes(b'outside root')
        linked = False
        try:
            (client / 'outside.zip').symlink_to(sibling / 'secret.zip')
            linked = True
        except OSError:
            print('SKIP filesystem symlink case: symlink creation is unavailable')

        with socket.socket() as port_socket:
            port_socket.bind(('127.0.0.1', 0))
            port = port_socket.getsockname()[1]
        base = f'http://127.0.0.1:{port}/SQLupdate/'
        env = os.environ.copy()
        env.update(TYTAN_API_USERNAME='test-user', TYTAN_API_PASSWORD='test-password', TYTAN_ALLOW_LOCAL_HTTP='1')
        env.pop('TYTAN_UPDATE_ROOT', None)
        authorization = 'Basic ' + base64.b64encode(b'test-user:test-password').decode()
        flags = subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0

        def request(endpoint, auth=authorization, method='GET'):
            headers = {} if auth is None else {'Authorization': auth}
            query = urllib.request.Request(base + endpoint, headers=headers, method=method)
            try:
                response = urllib.request.urlopen(query, timeout=5)
            except urllib.error.HTTPError as error:
                response = error
            with response:
                return response.status, response.headers, response.read()

        with (workspace / 'php.log').open('wb') as log:
            server = subprocess.Popen([php, '-S', f'127.0.0.1:{port}', '-t', str(workspace)],
                                      env=env, stdout=log, stderr=log, creationflags=flags)
            try:
                deadline = time.monotonic() + 10
                while True:
                    try:
                        request('api.php')
                        break
                    except (OSError, urllib.error.URLError):
                        if server.poll() is not None or time.monotonic() > deadline:
                            raise RuntimeError('PHP test server did not start')
                        time.sleep(0.05)

                count = 0

                def expect(endpoint, status, auth=authorization, method='GET'):
                    nonlocal count
                    result = request(endpoint, auth, method)
                    assert result[0] == status, (endpoint, result[0], result[2])
                    if status >= 400:
                        assert 'error' in json.loads(result[2])
                    count += 1
                    return result

                expect('api.php', 401, None)
                expect('download.php?file=cliente/Faktury_008.000.043.zip', 401, None)
                expect('api.php', 401, 'Basic ' + base64.b64encode(b'test-user:wrong').decode())
                expect('api.php', 401, 'Bearer invalid')
                expect('api.php', 405, method='POST')
                listing = json.loads(expect('api.php', 200)[2])
                assert {entry['name'] for entry in listing} == {'cliente', 'Empty', 'Client & more'}
                listing = json.loads(expect('api.php?dir=cliente', 200)[2])
                assert {entry['name'] for entry in listing} == {
                    'Faktury_008.000.043.zip', 'Faktury_008.000.040.zip', 'FK2025_999.000.000.zip'}
                assert all(set(entry) == {'name', 'type', 'size', 'modified', 'path'} for entry in listing)
                assert json.loads(expect('api.php?dir=Empty', 200)[2]) == []
                expect('api.php?dir=missing', 404)
                expect('api.php?dir=cliente/Faktury_008.000.043.zip', 404)
                for parameter in ['dir[]=cliente', 'dir=..', 'dir=../SQLupdate-other', 'dir=%2Fetc',
                                  'dir=C%3A%5Ctemp', 'dir=cliente%5C..', 'dir=.hidden', 'dir=cliente%00']:
                    expect('api.php?' + parameter, 400)
                for parameter in ['file=', 'file[]=x', 'file=../SQLupdate-other/secret.zip', 'file=cliente/.hidden.zip']:
                    expect('download.php?' + parameter, 400)
                for name in ['api.php', 'common.php', 'cliente/notes.txt', 'cliente/missing.zip', 'cliente']:
                    expect('download.php?file=' + urllib.parse.quote(name), 404)
                status, headers, body = expect('download.php?file=cliente/Faktury_008.000.043.zip', 200)
                assert body == (client / 'Faktury_008.000.043.zip').read_bytes()
                assert int(headers['Content-Length']) == len(body)
                assert headers['Content-Type'] == 'application/octet-stream'
                assert 'attachment;' in headers['Content-Disposition']
                assert headers['X-Content-Type-Options'] == 'nosniff'
                _, headers, body = expect('download.php?file=cliente/Faktury_008.000.043.zip', 200, method='HEAD')
                assert body == b'' and int(headers['Content-Length']) > 0
                expect('api.php?dir=' + urllib.parse.quote('Client & more'), 200)
                expect('download.php?file=' + urllib.parse.quote('Client & more/Faktury_008.000.043.zip'), 200)
                if linked:
                    expect('download.php?file=cliente/outside.zip', 404)

                # Verify boundary checking directly, including a sibling with a shared prefix.
                check = ("define('TYTAN_ENDPOINT', true); require " + json.dumps(str(root / 'common.php')) + '; '
                         "if (inside_root(" + json.dumps(str(sibling)) + ', ' + json.dumps(str(root)) + ')) exit(1);')
                subprocess.run([php, '-r', check], check=True)
                count += 1

                # Encoding failures must return generic JSON and log details privately.
                encoding_check = (
                    "define('TYTAN_ENDPOINT', true); require " + json.dumps(str(root / 'common.php')) + '; '
                    "$_SERVER['REQUEST_METHOD']='GET'; $_SERVER['HTTPS']='on'; "
                    "$_SERVER['PHP_AUTH_USER']='test-user'; $_SERVER['PHP_AUTH_PW']='test-password'; "
                    "bootstrap(); send_json(['bad'=>chr(255)]);"
                )
                encoding_script = workspace / 'encoding-check.php'
                encoding_script.write_text('<?php ' + encoding_check, encoding='utf-8')
                encoded = subprocess.run([php, str(encoding_script)], env=env,
                                         capture_output=True, check=True)
                assert json.loads(encoded.stdout) == {'error': 'The server could not complete the request.'}
                assert b'RuntimeException: JSON encoding failed:' in encoded.stderr
                count += 1

                # Exercise configuration failures with fresh server processes.
                def configuration_case(overrides, expected):
                    nonlocal base
                    case_env = env.copy()
                    for key, value in overrides.items():
                        if value is None:
                            case_env.pop(key, None)
                        else:
                            case_env[key] = value
                    with socket.socket() as available:
                        available.bind(('127.0.0.1', 0))
                        case_port = available.getsockname()[1]
                    saved_base = base
                    base = f'http://127.0.0.1:{case_port}/SQLupdate/'
                    case_server = subprocess.Popen([php, '-S', f'127.0.0.1:{case_port}', '-t', str(workspace)],
                                                   env=case_env, stdout=log, stderr=log, creationflags=flags)
                    try:
                        deadline = time.monotonic() + 10
                        while True:
                            try:
                                request('api.php')
                                break
                            except (OSError, urllib.error.URLError):
                                if case_server.poll() is not None or time.monotonic() > deadline:
                                    raise RuntimeError('Configuration test server did not start')
                                time.sleep(0.05)
                        expect('api.php', expected)
                    finally:
                        case_server.terminate()
                        try:
                            case_server.wait(timeout=5)
                        except subprocess.TimeoutExpired:
                            case_server.kill()
                            case_server.wait()
                        base = saved_base

                configuration_case({'TYTAN_ALLOW_LOCAL_HTTP': '0'}, 403)
                configuration_case({'TYTAN_API_USERNAME': None, 'TYTAN_API_PASSWORD': None}, 503)
                configuration_case({'TYTAN_API_PASSWORD': None}, 503)
                configuration_case({'TYTAN_UPDATE_ROOT': str(workspace / 'missing-root')}, 503)
                print(f'PASS {count} real-PHP endpoint checks', flush=True)

                if args.with_client:
                    client_env = os.environ.copy()
                    client_env['TYTAN_TEST_PHP_BASE_URL'] = f'http://127.0.0.1:{port}/'
                    subprocess.run(['dotnet', 'run', '--project', str(repo / 'tests/Tytan.Updater.Tests'),
                                    '--configuration', 'Release'], env=client_env, check=True)
            finally:
                server.terminate()
                try:
                    server.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    server.kill()
                    server.wait()


if __name__ == '__main__':
    main()
