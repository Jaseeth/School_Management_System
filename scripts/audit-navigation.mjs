import fs from 'node:fs';
import path from 'node:path';
const root = path.resolve(import.meta.dirname, '..');
const sourceRoot = path.join(root, 'school-management-web', 'src');
function files(directory) { return fs.readdirSync(directory,{withFileTypes:true}).flatMap(entry => entry.isDirectory() ? files(path.join(directory,entry.name)) : [path.join(directory,entry.name)]); }
const routesSource = fs.readFileSync(path.join(sourceRoot,'routes','AppRoutes.jsx'),'utf8');
const routes = [...routesSource.matchAll(/path="([^"]+)"/g)].map(m=>m[1]);
const matches = route => routes.filter(r=>r!=='*').some(pattern => new RegExp('^'+pattern.replace(/:[^/]+/g,'[^/]+')+'/?$').test(route));
const links = [], forms = [];
for(const file of files(sourceRoot).filter(f=>/\.[jt]sx?$/.test(f))) {
  const source=fs.readFileSync(file,'utf8');
  for(const match of source.matchAll(/(?:\bto|\bhref|\bpath)\s*[:=]\s*["'](\/[^"']*)["']/g)) {
    // Route declarations are definitions, not clickable links to be audited.
    if(file.endsWith(path.join('routes','AppRoutes.jsx')) && /^path\s*=/.test(match[0])) continue;
    const target=match[1].split(/[?#]/)[0];
    if(target.startsWith('/api') || target.includes('${')) continue;
    links.push({file:path.relative(root,file).replaceAll('\\','/'),target,line:source.slice(0,match.index).split('\n').length,matched:matches(target)});
  }
  for(const match of source.matchAll(/\bnavigate\(\s*["'](\/[^"']*)["']/g)) {
    const target=match[1].split(/[?#]/)[0];
    links.push({file:path.relative(root,file).replaceAll('\\','/'),target,line:source.slice(0,match.index).split('\n').length,matched:matches(target)});
  }
  if(source.includes('<form')) forms.push({file:path.relative(root,file).replaceAll('\\','/'),forms:[...source.matchAll(/<form\b/g)].length,buttons:[...source.matchAll(/<button\b/g)].length,coverage:'Inventory only; see browser and integration evidence for execution coverage.'});
}
const result={routes,links,unmatched:links.filter(l=>!l.matched),forms};
const output=path.join(root,'docs','PHASE1_NAVIGATION_INVENTORY.json');
fs.writeFileSync(output,JSON.stringify(result,null,2));
console.log(JSON.stringify({routes:routes.length,links:links.length,unmatched:result.unmatched,formFiles:forms.length},null,2));
if(result.unmatched.length) process.exitCode=1;
