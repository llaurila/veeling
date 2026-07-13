#!/usr/bin/env node

import { execFileSync } from "node:child_process";
import { basename } from "node:path";

const tarballs = process.argv.slice(2);
if (tarballs.length === 0) {
  console.error("Usage: node scripts/verify-npm-tarball-contents.mjs <tarball...>");
  process.exit(2);
}

const deniedSubstrings = [
  ".agent/",
  ".opencode/",
  ".openai/",
  ".claude/",
  ".gemini/",
  "process/",
  "memory/",
  "synapses/",
  ".feynman/"
];

for (const tarball of tarballs) {
  const listOutput = execFileSync("tar", ["-tzf", tarball], { encoding: "utf8" });
  const files = listOutput
    .split(/\r?\n/)
    .map((entry) => entry.trim())
    .filter(Boolean);

  if (files.length === 0) {
    console.error(`Tarball appears empty: ${tarball}`);
    process.exit(1);
  }

  for (const file of files) {
    for (const denied of deniedSubstrings) {
      if (file.includes(denied)) {
        console.error(`Denied content found in ${basename(tarball)}: ${file}`);
        process.exit(1);
      }
    }
  }
}

console.log(`Verified npm tarball contents for ${tarballs.length} package(s).`);
