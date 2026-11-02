import { cp, mkdir, readFile, writeFile } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';

const cssPath = new URL('../src/styles.css', import.meta.url);
const original = await readFile(cssPath, 'utf8');
const run = () => spawnSync(process.execPath, ['node_modules/@playwright/test/cli.js', 'test'], { stdio: 'inherit' });
try {
  await writeFile(cssPath, original.replace('font-size: 32px; line-height: 40px; letter-spacing: -1px', 'font-size: 36px; line-height: 40px; letter-spacing: -1px'));
  const red = spawnSync(process.execPath, ['node_modules/@playwright/test/cli.js', 'test', '--reporter=json'], { encoding: 'utf8' });
  if (red.error || red.status !== 1) throw new Error('Навмисна зміна стилю повинна зламати порівняння з еталоном.');
  const report = JSON.parse(red.stdout);
  if (!report.stats.unexpected || !red.stdout.includes('toHaveScreenshot')) throw new Error('Прогін повинен падати саме на порівнянні знімків.');
  await mkdir(new URL('../artifacts', import.meta.url), { recursive: true });
  await writeFile(new URL('../artifacts/visual-proof-red.json', import.meta.url), red.stdout);
  await cp(new URL('../test-results', import.meta.url), new URL('../artifacts/visual-proof-red', import.meta.url), { recursive: true });
  console.log('Підтверджено: навмисна зміна стилю порушує еталони.');
} finally {
  await writeFile(cssPath, original);
}
if (run().status !== 0) process.exitCode = 1;
