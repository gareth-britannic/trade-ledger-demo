import { spawn } from 'node:child_process'
import { readdir, readFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url))
const webRoot = path.resolve(scriptDirectory, '..')
const generatedRoot = path.join(webRoot, 'src', 'api', 'generated')

async function snapshot(directory, root = directory) {
  const files = new Map()
  let entries

  try {
    entries = await readdir(directory, { withFileTypes: true })
  } catch (error) {
    if (error?.code === 'ENOENT') return files
    throw error
  }

  for (const entry of entries.sort((left, right) => left.name.localeCompare(right.name))) {
    const absolutePath = path.join(directory, entry.name)
    if (entry.isDirectory()) {
      const nested = await snapshot(absolutePath, root)
      for (const [name, contents] of nested) files.set(name, contents)
    } else if (entry.isFile()) {
      files.set(path.relative(root, absolutePath), await readFile(absolutePath, 'utf8'))
    }
  }

  return files
}

const runGeneration = () =>
  new Promise((resolve, reject) => {
    const executable = process.platform === 'win32' ? 'npm.cmd' : 'npm'
    const child = spawn(executable, ['run', 'api:generate'], {
      cwd: webRoot,
      stdio: 'inherit',
    })
    child.once('error', reject)
    child.once('exit', (code, signal) => {
      if (signal) reject(new Error(`API generation stopped with signal ${signal}.`))
      else resolve(code ?? 1)
    })
  })

const before = await snapshot(generatedRoot)
const exitCode = await runGeneration()
if (exitCode !== 0) process.exit(exitCode)
const after = await snapshot(generatedRoot)

const paths = [...new Set([...before.keys(), ...after.keys()])].sort()
const changed = paths.filter((filePath) => before.get(filePath) !== after.get(filePath))

if (changed.length > 0) {
  console.error('Generated API client drift detected:')
  for (const filePath of changed) console.error(`- src/api/generated/${filePath}`)
  console.error('Run npm run api:generate and commit the generated changes.')
  process.exitCode = 1
} else {
  console.log('Generated API client matches the versioned OpenAPI snapshot.')
}
