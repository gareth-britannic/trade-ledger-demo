import { mkdir, writeFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url))
const webRoot = path.resolve(scriptDirectory, '..')
const snapshotPath = path.join(webRoot, 'openapi', 'trade-ledger.v1.json')
const source = 'http://127.0.0.1:5232/swagger/v1/swagger.json'

const response = await fetch(source, {
  headers: { Accept: 'application/json' },
})

if (!response.ok) {
  throw new Error(`OpenAPI download failed with HTTP ${response.status}.`)
}

const document = await response.json()

if (document?.info?.version !== 'v1' || typeof document?.paths !== 'object') {
  throw new Error('The downloaded document is not the expected Trade Ledger v1 contract.')
}

await mkdir(path.dirname(snapshotPath), { recursive: true })
await writeFile(snapshotPath, `${JSON.stringify(document, null, 2)}\n`, 'utf8')

console.log(`Updated ${path.relative(webRoot, snapshotPath)} from ${source}.`)
