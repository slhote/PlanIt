import { chromium, devices } from '@playwright/test'
import { mkdir, readFile } from 'node:fs/promises'
import { createWriteStream } from 'node:fs'
import path from 'node:path'
import { pathToFileURL } from 'node:url'
import { PNG } from 'pngjs'
import pixelmatch from 'pixelmatch'

const STANDARD_VIEWPORTS = {
  desktop: { label: 'desktop', device: { viewport: { width: 1440, height: 900 } } },
  'iphone-14-portrait': { label: 'iphone-14-portrait', device: devices['iPhone 14'] },
  'iphone-14-landscape': { label: 'iphone-14-landscape', device: devices['iPhone 14 landscape'] },
  'pixel-8-portrait': { label: 'pixel-8-portrait', device: devices['Pixel 8'] },
  'pixel-8-landscape': { label: 'pixel-8-landscape', device: devices['Pixel 8 landscape'] },
}

const COLOR_SCHEMES = ['light', 'dark']

async function diffAgainstBaseline(candidatePath, baselinePath) {
  let baselineBuf
  try {
    baselineBuf = await readFile(baselinePath)
  } catch {
    return { baseline: baselinePath, status: 'no-baseline' }
  }
  const candidateBuf = await readFile(candidatePath)
  const baselinePng = PNG.sync.read(baselineBuf)
  const candidatePng = PNG.sync.read(candidateBuf)

  if (baselinePng.width !== candidatePng.width || baselinePng.height !== candidatePng.height) {
    return {
      baseline: baselinePath,
      status: 'size-mismatch',
      baselineSize: { width: baselinePng.width, height: baselinePng.height },
      candidateSize: { width: candidatePng.width, height: candidatePng.height },
    }
  }

  const { width, height } = baselinePng
  const diffPng = new PNG({ width, height })
  const numDiffPixels = pixelmatch(baselinePng.data, candidatePng.data, diffPng.data, width, height, {
    threshold: 0.1,
  })
  const diffPath = candidatePath.replace(/\.png$/, '-diff.png')
  if (numDiffPixels > 0) {
    await new Promise((resolve, reject) => {
      const stream = createWriteStream(diffPath)
      diffPng.pack().pipe(stream)
      stream.on('finish', resolve)
      stream.on('error', reject)
    })
  }
  return {
    baseline: baselinePath,
    status: numDiffPixels === 0 ? 'match' : 'diff',
    numDiffPixels,
    diffRatio: numDiffPixels / (width * height),
    diffPath: numDiffPixels > 0 ? diffPath : null,
  }
}

/**
 * Captures screenshots of a URL across the standard desktop/iPhone 14/Pixel 8 viewport matrix.
 * @param {object} opts
 * @param {string} opts.url - Page to capture.
 * @param {string} opts.out - Output directory for screenshots.
 * @param {string} opts.name - Base filename prefix for this batch.
 * @param {string[]} [opts.viewports] - Subset of STANDARD_VIEWPORTS keys to capture (default: all).
 * @param {'light'|'dark'|'both'} [opts.colorScheme] - Color scheme(s) to capture (default: 'light').
 * @param {string} [opts.baseline] - Directory of previously saved screenshots to pixel-diff
 *   against (matched by `<name>-<viewport>[-<colorScheme>].png`). Omit to skip diffing.
 * @param {(page: import('@playwright/test').Page, ctx: { viewport: string, colorScheme: string }) => Promise<void>} [opts.interact] -
 *   Optional callback run after navigation and before the screenshot, for UI states that require
 *   interaction (click, drag, fill, hover, waitForSelector, etc.) to reach.
 * @param {'load'|'domcontentloaded'|'networkidle'|'commit'} [opts.waitUntil] - Navigation wait
 *   strategy passed to `page.goto` (default: 'load'). Avoid 'networkidle' for pages that keep a
 *   connection open (SignalR, WebSockets, polling) — it can hang or fire early since the
 *   connection never truly goes idle. Prefer 'load' plus `waitForSelector` for content readiness.
 * @param {string} [opts.waitForSelector] - CSS selector to wait for after navigation (and after
 *   `interact`, if provided) before capturing — use this instead of `networkidle` to know the UI
 *   is actually ready.
 * @param {number} [opts.timeout] - Timeout in ms for navigation and `waitForSelector` (default: 15000).
 * @returns {Promise<Array<{path: string, viewport: string, colorScheme: string, consoleErrors: string[], pageErrors: string[], diff: object|null}>>}
 */
export async function captureUI({
  url,
  out,
  name,
  viewports,
  colorScheme,
  baseline,
  interact,
  waitUntil,
  waitForSelector,
  timeout,
}) {
  if (!url) throw new Error('captureUI: url is required')
  if (!out) throw new Error('captureUI: out is required')
  if (!name) throw new Error('captureUI: name is required')

  const navTimeout = timeout || 15000

  const selectedViewports = (viewports && viewports.length ? viewports : Object.keys(STANDARD_VIEWPORTS)).map(
    (key) => {
      const entry = STANDARD_VIEWPORTS[key]
      if (!entry) {
        throw new Error(
          `Unknown viewport "${key}". Valid options: ${Object.keys(STANDARD_VIEWPORTS).join(', ')}`
        )
      }
      return entry
    }
  )

  const schemes = colorScheme === 'both' ? COLOR_SCHEMES : [colorScheme || 'light']
  for (const scheme of schemes) {
    if (!COLOR_SCHEMES.includes(scheme)) {
      throw new Error(`Unknown colorScheme "${scheme}". Valid options: ${COLOR_SCHEMES.join(', ')}, both`)
    }
  }

  await mkdir(out, { recursive: true })

  const browser = await chromium.launch()
  const results = []

  try {
    for (const scheme of schemes) {
      for (const { label, device } of selectedViewports) {
        const viewportLabel = schemes.length > 1 ? `${label}-${scheme}` : label
        const context = await browser.newContext({ ...device, colorScheme: scheme })
        const page = await context.newPage()

        const consoleErrors = []
        const pageErrors = []
        page.on('console', (msg) => {
          if (msg.type() === 'error') consoleErrors.push(msg.text())
        })
        page.on('pageerror', (err) => {
          pageErrors.push(err.message)
        })
        page.on('requestfailed', (req) => {
          consoleErrors.push(`Request failed: ${req.method()} ${req.url()} — ${req.failure()?.errorText}`)
        })

        try {
          await page.goto(url, { waitUntil: waitUntil || 'load', timeout: navTimeout })
          if (waitForSelector) {
            await page.waitForSelector(waitForSelector, { timeout: navTimeout })
          }
          if (interact) {
            await interact(page, { viewport: label, colorScheme: scheme })
          }
          const filePath = path.join(out, `${name}-${viewportLabel}.png`)
          await page.screenshot({ path: filePath })

          let diff = null
          if (baseline) {
            const baselinePath = path.join(baseline, `${name}-${viewportLabel}.png`)
            diff = await diffAgainstBaseline(filePath, baselinePath)
          }

          results.push({
            path: filePath,
            viewport: viewportLabel,
            colorScheme: scheme,
            consoleErrors,
            pageErrors,
            diff,
          })
        } finally {
          await context.close()
        }
      }
    }
  } finally {
    await browser.close()
  }

  return results
}

function parseArgs(argv) {
  const args = { viewports: [] }
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i]
    if (arg === '--url') args.url = argv[++i]
    else if (arg === '--out') args.out = argv[++i]
    else if (arg === '--name') args.name = argv[++i]
    else if (arg === '--viewports') args.viewports = argv[++i].split(',').map((v) => v.trim())
    else if (arg === '--color-scheme') args.colorScheme = argv[++i]
    else if (arg === '--baseline') args.baseline = argv[++i]
    else if (arg === '--wait-until') args.waitUntil = argv[++i]
    else if (arg === '--wait-for-selector') args.waitForSelector = argv[++i]
    else if (arg === '--timeout') args.timeout = Number(argv[++i])
    else throw new Error(`Unknown argument: ${arg}`)
  }
  return args
}

const isMain = process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href
if (isMain) {
  const args = parseArgs(process.argv.slice(2))
  captureUI(args)
    .then((results) => {
      let hadIssues = false
      for (const r of results) {
        console.log(r.path)
        if (r.consoleErrors.length) {
          hadIssues = true
          console.log(`  console errors (${r.viewport}):`)
          for (const e of r.consoleErrors) console.log(`    - ${e}`)
        }
        if (r.pageErrors.length) {
          hadIssues = true
          console.log(`  page errors (${r.viewport}):`)
          for (const e of r.pageErrors) console.log(`    - ${e}`)
        }
        if (r.diff) {
          if (r.diff.status === 'diff') {
            hadIssues = true
            console.log(
              `  diff vs baseline (${r.viewport}): ${r.diff.numDiffPixels} px (${(r.diff.diffRatio * 100).toFixed(2)}%) -> ${r.diff.diffPath}`
            )
          } else if (r.diff.status === 'no-baseline') {
            console.log(`  diff vs baseline (${r.viewport}): no baseline found at ${r.diff.baseline}`)
          } else if (r.diff.status === 'size-mismatch') {
            hadIssues = true
            console.log(`  diff vs baseline (${r.viewport}): size mismatch`)
          }
        }
      }
      if (hadIssues) process.exitCode = 1
    })
    .catch((err) => {
      console.error(err)
      process.exit(1)
    })
}
