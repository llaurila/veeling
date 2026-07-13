#!/usr/bin/env node

import { readFileSync } from "node:fs";

const content = readFileSync("./Directory.Build.props", "utf8");
const match = content.match(/<VeelingVersion>([^<]+)<\/VeelingVersion>/i);

if (!match) {
  console.error("Unable to resolve <VeelingVersion> from Directory.Build.props");
  process.exit(1);
}

process.stdout.write(match[1].trim());
