const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const downloadPath = path.resolve(__dirname, '../../Assets/Art/Characters/Mixamo');

if (!fs.existsSync(downloadPath)) {
    fs.mkdirSync(downloadPath, { recursive: true });
}

async function run() {
    console.log(`\n[Scraper] Starting browser...`);
    console.log(`[Scraper] All downloads will automatically go to:\n  ${downloadPath}\n`);
    
    const browser = await chromium.launch({ 
        headless: false,
        args: ['--start-maximized']
    });
    const context = await browser.newContext({
        acceptDownloads: true,
        viewport: null
    });
    
    const page = await context.newPage();
    
    let downloadCount = 0;

    // Route ALL downloads to our Unity folder automatically
    page.on('download', async download => {
        downloadCount++;
        const suggestedName = download.suggestedFilename();
        const savePath = path.join(downloadPath, suggestedName);
        console.log(`[Scraper] Download #${downloadCount}: ${suggestedName}`);
        await download.saveAs(savePath);
        console.log(`[Scraper] SAVED to Unity: ${savePath}`);
    });

    await page.goto('https://www.mixamo.com/');
    
    console.log('=========================================================');
    console.log('  BROWSER IS OPEN. Please do the following:');
    console.log('');
    console.log('  1. Log in to your Adobe account.');
    console.log('  2. Download a CHARACTER:');
    console.log('     - Pick any character you like (Y Bot, Paladin, etc)');
    console.log('     - Click Download -> Format: FBX -> Pose: T-Pose');
    console.log('     - NAME THE FILE: Character.fbx before saving');
    console.log('');
    console.log('  3. Download these ANIMATIONS (Format: FBX, WITHOUT skin):');
    console.log('     - Idle       -> save as Idle.fbx');
    console.log('     - Walking    -> save as Walking.fbx');
    console.log('     - Running    -> save as Running.fbx');
    console.log('     - Jump       -> save as Jump.fbx');
    console.log('     - Attack     -> search "Standing Melee Attack" or similar');
    console.log('                     -> save as Attack.fbx');
    console.log('');
    console.log('  ALL files will save into the correct Unity folder automatically!');
    console.log('');
    console.log('  WHEN DONE: Simply CLOSE THE BROWSER TAB or the browser window.');
    console.log('=========================================================\n');

    // Wait until the browser/page is closed by the user
    await page.waitForEvent('close', { timeout: 0 }).catch(() => {});
    
    console.log(`\n[Scraper] Browser closed. Total files downloaded: ${downloadCount}`);
    
    if (downloadCount === 0) {
        console.log('[Scraper] WARNING: No files were downloaded. Please re-run the script and try again.');
    } else {
        console.log('[Scraper] Files saved to:');
        fs.readdirSync(downloadPath).forEach(f => console.log(`  - ${f}`));
    }

    await browser.close().catch(() => {});
    console.log('[Scraper] Done!');
}

run().catch(err => {
    console.error('[Scraper] Fatal error:', err.message);
    process.exit(1);
});
