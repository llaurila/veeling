#!/usr/bin/env node

const { spawnSync } = require("node:child_process");
const path = require("node:path");
const fs = require("node:fs");

const packageRoot = path.resolve(__dirname, "..");
const platform = process.platform;
const arch = process.arch;

const packageByPlatform = {
  "win32:x64": "@veeling/cli-win32-x64",
  "linux:x64": "@veeling/cli-linux-x64",
  "darwin:x64": "@veeling/cli-darwin-x64",
  "darwin:arm64": "@veeling/cli-darwin-arm64"
};

const key = `${platform}:${arch}`;
const platformPackage = packageByPlatform[key];

if (!platformPackage) {
  console.error(`[veeling] Unsupported platform for @veeling/cli: ${platform}/${arch}.`);
  console.error("[veeling] Supported combinations: win32/x64, linux/x64, darwin/x64, darwin/arm64.");
  process.exit(1);
}

let resolved;
try {
  resolved = require.resolve(`${platformPackage}/package.json`, { paths: [packageRoot] });
} catch (error) {
  console.error(`[veeling] Platform package '${platformPackage}' is not installed.`);
  console.error("[veeling] Re-run npm install and confirm optional dependencies are enabled.");
  process.exit(1);
}

const packageJson = JSON.parse(fs.readFileSync(resolved, "utf8"));
const relativeExecutable = packageJson.veelingBinary;
if (!relativeExecutable || typeof relativeExecutable !== "string") {
  console.error(`[veeling] Invalid platform package metadata for '${platformPackage}'.`);
  process.exit(1);
}

const executablePath = path.resolve(path.dirname(resolved), relativeExecutable);
if (!fs.existsSync(executablePath)) {
  console.error(`[veeling] Platform binary not found: ${executablePath}`);
  process.exit(1);
}

const result = spawnSync(executablePath, process.argv.slice(2), {
  stdio: "inherit",
  windowsHide: true
});

if (result.error) {
  console.error(`[veeling] Failed to launch binary: ${result.error.message}`);
  process.exit(1);
}

if (typeof result.status === "number") {
  process.exit(result.status);
}

process.exit(1);
