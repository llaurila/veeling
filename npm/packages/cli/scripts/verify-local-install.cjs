#!/usr/bin/env node

const { spawnSync } = require("node:child_process");

const result = spawnSync("node", ["./bin/veeling.cjs", "--version"], {
  stdio: "inherit",
  windowsHide: true
});

if (result.error) {
  console.error(result.error.message);
  process.exit(1);
}

process.exit(typeof result.status === "number" ? result.status : 1);
