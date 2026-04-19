import { chromium } from 'playwright';

const BASE = 'http://localhost:8096';
const USER = 'root';
const PASS = 'test';
const OUT  = 'docs/screenshots';

async function authenticate(page) {
    // Hit the quick-connect / auth endpoint so the browser has a valid session cookie
    await page.goto(`${BASE}/web/index.html`);

    // Fill the login form
    await page.waitForSelector('input[name="txtManualName"], input#txtManualName, input[placeholder*="name" i], input[type="text"]', { timeout: 15000 });
    await page.fill('input[type="text"]', USER);
    await page.fill('input[type="password"]', PASS);
    await page.click('button[type="submit"], .raised.button-submit');
    await page.waitForURL(`${BASE}/web/**`, { timeout: 15000 });
    await page.waitForTimeout(1500);
}

async function navigateToPage(page, hash, readySelector, waitMs = 2000) {
    await page.goto(`${BASE}/web/index.html#!${hash}`);
    await page.waitForSelector(readySelector, { timeout: 20000 });
    await page.waitForTimeout(waitMs);
}

(async () => {
    const browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({
        viewport: { width: 1440, height: 900 },
        colorScheme: 'dark',
    });
    const page = await context.newPage();

    console.log('Authenticating...');
    await authenticate(page);

    // ── Main page (Age Ratings) ──────────────────────────────────────────────
    console.log('Capturing main page...');
    await navigateToPage(
        page,
        '/configurationpage?name=Age%20Ratings%20Main',
        '#AgeRatingsMainPage',
        3000
    );
    await page.screenshot({ path: `${OUT}/main-page.png`, fullPage: false });
    console.log(`Saved ${OUT}/main-page.png`);

    // ── Config page (mappings & settings) ────────────────────────────────────
    console.log('Capturing config page...');
    await navigateToPage(
        page,
        '/configurationpage?name=Age%20Rating%20Converter',
        '#AgeRatingConfigPage',
        2500
    );
    await page.screenshot({ path: `${OUT}/config-page.png`, fullPage: false });
    console.log(`Saved ${OUT}/config-page.png`);

    await browser.close();
    console.log('Done.');
})();
