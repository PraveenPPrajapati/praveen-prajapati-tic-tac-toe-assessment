// Exports the trusted ASP.NET Core development certificate as PEM files so the
// Angular dev server can serve https://localhost:4200 without browser warnings.
// Runs automatically before `npm start`; does nothing if the files already exist.
import { existsSync, mkdirSync } from 'node:fs';
import { execFileSync } from 'node:child_process';

const certPath = '.certs/localhost.pem';
const keyPath = '.certs/localhost.key';

if (existsSync(certPath) && existsSync(keyPath)) process.exit(0);

mkdirSync('.certs', { recursive: true });
console.log('Exporting the .NET HTTPS development certificate for the Angular dev server...');
try {
  execFileSync('dotnet', ['dev-certs', 'https', '--export-path', certPath, '--format', 'Pem', '--no-password'],
    { stdio: 'inherit' });
} catch {
  console.error('Could not export the certificate. Run `dotnet dev-certs https --trust` and try again.');
  process.exit(1);
}
