#!/usr/bin/env node

import { mkdtempSync, rmSync, writeFileSync, readFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { execFileSync } from "node:child_process";

const tarballs = process.argv.slice(2);
if (tarballs.length === 0) {
  console.error("Usage: node scripts/smoke-npm-install.mjs <tarball...>");
  process.exit(2);
}

const workspace = mkdtempSync(join(tmpdir(), "veeling-npm-smoke-"));

try {
  const packageJsonPath = join(workspace, "package.json");
  writeFileSync(
    packageJsonPath,
    JSON.stringify(
      {
        name: "veeling-npm-smoke",
        private: true,
        version: "0.0.0",
        devDependencies: {}
      },
      null,
      2
    ) + "\n",
    "utf8"
  );

  for (const tarball of tarballs) {
    execFileSync("npm", ["install", "--save-dev", tarball], {
      cwd: workspace,
      stdio: "inherit"
    });
  }

  const packageJson = JSON.parse(readFileSync(packageJsonPath, "utf8"));
  const expectedVersion = String(packageJson.devDependencies["@veeling/cli"] || "").replace(/^[^\d]*/, "");

  const output = execFileSync("npx", ["veeling", "--version"], {
    cwd: workspace,
    encoding: "utf8"
  }).trim();

  if (expectedVersion && output !== expectedVersion) {
    throw new Error(`Version mismatch from npx veeling --version: expected ${expectedVersion}, got ${output}`);
  }

  console.log(`Smoke install succeeded in ${workspace}. Reported version: ${output}`);
} finally {
  rmSync(workspace, { recursive: true, force: true });
}
