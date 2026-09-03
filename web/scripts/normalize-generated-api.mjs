import { readdir, readFile, writeFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url))
const generatedRoot = path.resolve(scriptDirectory, '..', 'src', 'api', 'generated')

async function generatedFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true })
  const files = await Promise.all(
    entries.map(async (entry) => {
      const absolutePath = path.join(directory, entry.name)
      return entry.isDirectory() ? generatedFiles(absolutePath) : [absolutePath]
    }),
  )

  return files.flat().filter((filePath) => filePath.endsWith('.ts'))
}

for (const filePath of await generatedFiles(generatedRoot)) {
  const source = await readFile(filePath, 'utf8')
  const normalized = `${source.replace(/[ \t]+$/gmu, '').trimEnd()}\n`
  if (normalized !== source) await writeFile(filePath, normalized)
}
