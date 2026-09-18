// scripts/generate-icon-types.ts
import * as icons from '@element-plus/icons-vue';
import fs from 'fs';

const iconNames = Object.keys(icons);
const dts = `import type * as Icons from '@element-plus/icons-vue';
import '@vue/runtime-core';

declare module 'vue' {
  export interface GlobalComponents {
${iconNames.map(name => `    'ele-${name}': typeof Icons.${name}`).join('\n')}
  }
}`;

fs.writeFileSync('src/types/ele-icons.d.ts', dts);