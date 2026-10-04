import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import Ajv2020 from 'ajv/dist/2020.js';

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(scriptDirectory, '../..');

async function readJson(relativePath) {
  const absolutePath = path.isAbsolute(relativePath) ? relativePath : path.join(repositoryRoot, relativePath);
  return JSON.parse(await readFile(absolutePath, 'utf8'));
}

function getArgumentPath(name, fallback) {
  const index = process.argv.indexOf(name);
  if (index < 0) return fallback;
  const value = process.argv[index + 1];
  if (!value || value.startsWith('--')) throw new Error(`${name} requires a file path.`);
  return path.resolve(process.cwd(), value);
}

function formatErrors(errors) {
  return (errors ?? [])
    .map((error) => `${error.instancePath || '/'} ${error.message} (${JSON.stringify(error.params)})`)
    .join('\n');
}

const ajv = new Ajv2020({ allErrors: true, strict: false });
ajv.addSchema(await readJson('shared-schema/card-data/card-definition.schema.json'));

const registries = [
  {
    label: 'Card definition registry',
    schemaPath: 'shared-schema/card-data/card-definition-registry.schema.json',
    argument: '--definitions',
    documentPath: 'shared-schema/card-data/card-definition-registry.v1.json',
  },
  {
    label: 'Implemented effect registry',
    schemaPath: 'shared-schema/card-data/implemented-effect-registry.schema.json',
    argument: '--effects',
    documentPath: 'shared-schema/card-data/implemented-effect-registry.v1.json',
  },
  {
    label: 'Card name registry',
    schemaPath: 'shared-schema/card-data/localization/card-name-registry.schema.json',
    argument: '--names',
    documentPath: 'shared-schema/card-data/localization/card-name-registry.zh-CN.v1.json',
  },
  {
    label: 'Card text registry',
    schemaPath: 'shared-schema/card-data/localization/card-text-registry.schema.json',
    argument: '--texts',
    documentPath: 'shared-schema/card-data/localization/card-text-registry.zh-CN.v1.json',
  },
  {
    label: 'Card theme registry',
    schemaPath: 'shared-schema/card-data/card-theme-registry.schema.json',
    argument: '--themes',
    documentPath: 'shared-schema/card-data/card-theme-registry.v1.json',
  },
  {
    label: 'Card art registry',
    schemaPath: 'shared-schema/card-art/card-art-registry.schema.json',
    argument: '--art',
    documentPath: 'shared-schema/card-art/card-art-registry.v1.json',
  },
  {
    label: 'Minecraft asset source',
    schemaPath: 'shared-schema/card-art/minecraft-asset-source.schema.json',
    argument: '--asset-source',
    documentPath: 'shared-schema/card-art/minecraft-asset-source.v1.json',
  },
  {
    label: 'Minecraft world texture registry',
    schemaPath: 'shared-schema/card-art/minecraft-world-texture-registry.schema.json',
    argument: '--world-textures',
    documentPath: 'shared-schema/card-art/minecraft-world-texture-registry.v1.json',
  },
  {
    label: 'Bedrock entity source',
    schemaPath: 'shared-schema/card-art/bedrock-entity-source.schema.json',
    argument: '--bedrock-source',
    documentPath: 'shared-schema/card-art/bedrock-entity-source.v1.json',
  },
];

const documents = new Map();
for (const registry of registries) {
  const schema = await readJson(registry.schemaPath);
  const document = await readJson(getArgumentPath(registry.argument, registry.documentPath));
  documents.set(registry.label, document);
  const validate = ajv.compile(schema);
  if (!validate(document)) {
    process.stderr.write(`${registry.label} does not match its JSON Schema:\n${formatErrors(validate.errors)}\n`);
    process.exitCode = 1;
  }
}

const definitionVersion = documents.get('Card definition registry').implementedEffectRegistryVersion;
const effectsVersion = documents.get('Implemented effect registry').contentVersion;
if (definitionVersion !== effectsVersion) {
  process.stderr.write(`Card definition registry references implemented-effect version ${definitionVersion}, but the effect registry is at ${effectsVersion}. Regenerate card content.\n`);
  process.exitCode = 1;
}

const bedrockSource = documents.get('Bedrock entity source');
const geometryKeys = Object.keys(bedrockSource.entityGeometry).sort();
const textureKeys = Object.keys(bedrockSource.entityTextures).sort();
if (JSON.stringify(geometryKeys) !== JSON.stringify(textureKeys)) {
  process.stderr.write('Bedrock entity source geometry and texture maps must contain the same entity keys.\n');
  process.exitCode = 1;
}

if (process.exitCode !== 1) {
  const definitions = documents.get('Card definition registry');
  const effects = documents.get('Implemented effect registry');
  const names = documents.get('Card name registry');
  const texts = documents.get('Card text registry');
  const themes = documents.get('Card theme registry');
  const art = documents.get('Card art registry');
  const worldTextures = documents.get('Minecraft world texture registry');
  process.stdout.write(`Card JSON Schemas passed: ${definitions.entries.length} definitions, ${effects.implementedEffectIds.length} implemented effect ids, ${names.entries.length} names/texts, ${themes.themes.length} themes, ${art.entries.length} art mappings, ${worldTextures.entries.length} world textures, and Java/Bedrock source provenance.\n`);
}
