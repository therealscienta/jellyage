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
        '/configurationpage?name=AgeRatings',
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
    // Unscrolled, the page's intro text pushes the mapping table entirely below the
    // 900px fold and the shot ends at the column headers. Anchor on the Settings card
    // instead: that frames target-system choice, the mapping actions and the unmapped
    // worklist, with a few table rows to show it is editable. Anchoring on the mapping
    // card instead scrolls too far — the frame then fills with near-identical rating
    // rows and loses the settings context.
    await page.evaluate(() => {
        const card = document.querySelector('#AgeRatingConfigPage .arc-card');
        if (card) {
            // Sit the card just under Jellyfin's ~48px sticky bar. Any more headroom
            // and the intro paragraph reappears sliced through the middle of a line.
            window.scrollTo({ top: card.getBoundingClientRect().top + window.scrollY - 56 });
        }
    });
    await page.waitForTimeout(800);
    await page.screenshot({ path: `${OUT}/config-page.png`, fullPage: false });
    console.log(`Saved ${OUT}/config-page.png`);

    await browser.close();
    console.log('Done.');
})();
