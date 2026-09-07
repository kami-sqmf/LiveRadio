import { build } from 'esbuild';
import { mkdir, copyFile, readFile } from 'node:fs/promises';
const { version } = JSON.parse(await readFile('package.json', 'utf8'));
await mkdir('dist', { recursive: true });
await build({
  entryPoints: ['src/index.tsx'], outfile: 'dist/LiveRadio.mjs', bundle: true,
  format: 'esm', target: 'chrome80', minify: false,
  banner: { js: `/*\n * Cities: Skylines II UI Module\n * Id: LiveRadio\n * Author: LiveRadio\n * Version: ${version}\n * Dependencies:\n */` },
  plugins: [{ name: 'game-globals', setup(builder) {
    builder.onResolve({ filter: /^(react|cs2\/api|cs2\/ui|cs2\/input)$/ }, args => ({ path: args.path, namespace: 'game' }));
    builder.onLoad({ filter: /.*/, namespace: 'game' }, args => ({
      contents: `module.exports = window[${JSON.stringify(args.path === 'react' ? 'React' : args.path)}];`, loader: 'js'
    }));
  }}]
});
await copyFile('src/style.css', 'dist/LiveRadio.css');
console.log('Built native UI module: dist/LiveRadio.mjs + LiveRadio.css');
