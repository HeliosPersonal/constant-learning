import { readFile, writeFile } from 'node:fs/promises';

const source = new URL('../../docs/features/personal-trainer-mvp/design/theme-tokens.json', import.meta.url);
const tokens = JSON.parse(await readFile(source, 'utf8'));
const css = Object.entries(tokens.themes).map(([theme, values]) => {
  const declarations = Object.entries({ ...values, ...Object.fromEntries(Object.entries(tokens.aliases).map(([name, alias]) => [name, values[alias]])) })
    .map(([name, color]) => `  --cl-${name}: ${color};`).join('\n');
  return `:root[data-theme="${theme}"] {\n  color-scheme: ${theme};\n${declarations}\n}`;
}).join('\n\n');
await writeFile(new URL('../src/theme/tokens.css', import.meta.url), `/* auto-generated: npm run generate:tokens */\n${css}\n`);
