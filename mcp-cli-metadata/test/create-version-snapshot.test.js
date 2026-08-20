const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const os = require("node:os");
const path = require("node:path");
const test = require("node:test");

const {
    REQUIRED_ARTIFACTS,
    createVersionSnapshot,
} = require("../create-version-snapshot");

async function createTempRoot() {
    return fs.mkdtemp(path.join(os.tmpdir(), "mcp-cli-snapshot-test-"));
}

test("creates a version-named directory containing the four metadata artifacts", async (t) => {
    const rootDir = await createTempRoot();
    t.after(() => fs.rm(rootDir, { recursive: true, force: true }));

    const version = "3.0.0-beta.35+abcdef123456";
    let temporaryOutputDirectory;
    const outputDirectory = await createVersionSnapshot({
        rootDir,
        runExtractor: async (outputDirectory) => {
            temporaryOutputDirectory = outputDirectory;
            const cliDirectory = path.join(temporaryOutputDirectory, "cli");
            await fs.mkdir(cliDirectory, { recursive: true });
            await Promise.all(REQUIRED_ARTIFACTS.map((fileName) => {
                const contents = fileName === "cli-version.json"
                    ? JSON.stringify({ version })
                    : JSON.stringify({ fileName });
                return fs.writeFile(path.join(cliDirectory, fileName), contents);
            }));
        },
    });

    assert.equal(outputDirectory, path.join(rootDir, version));
    assert.deepEqual(
        (await fs.readdir(outputDirectory)).sort(),
        [...REQUIRED_ARTIFACTS].sort(),
    );
    assert.equal(
        JSON.parse(await fs.readFile(path.join(outputDirectory, "cli-version.json"), "utf8")).version,
        version,
    );
    assert.equal(
        await fs.readFile(path.join(rootDir, "tracked-version.txt"), "utf8"),
        "3.0.0-beta.35\n",
    );
    await assert.rejects(fs.access(temporaryOutputDirectory));
});

test("does not replace an existing version snapshot", async (t) => {
    const rootDir = await createTempRoot();
    t.after(() => fs.rm(rootDir, { recursive: true, force: true }));

    const version = "3.0.0-beta.35+abcdef123456";
    await fs.mkdir(path.join(rootDir, version));
    await fs.writeFile(path.join(rootDir, "tracked-version.txt"), "3.0.0-beta.34\n");
    let extractorCalled = false;

    await assert.rejects(
        createVersionSnapshot({
            rootDir,
            runExtractor: async (temporaryOutputDirectory) => {
                extractorCalled = true;
                const cliDirectory = path.join(temporaryOutputDirectory, "cli");
                await fs.mkdir(cliDirectory, { recursive: true });
                await Promise.all(REQUIRED_ARTIFACTS.map((fileName) => fs.writeFile(
                    path.join(cliDirectory, fileName),
                    fileName === "cli-version.json"
                        ? JSON.stringify({ version })
                        : "{}",
                )));
            },
        }),
        /already exists/,
    );
    assert.equal(extractorCalled, true);
    assert.equal(
        await fs.readFile(path.join(rootDir, "tracked-version.txt"), "utf8"),
        "3.0.0-beta.34\n",
    );
});

test("removes temporary output when an artifact is missing", async (t) => {
    const rootDir = await createTempRoot();
    t.after(() => fs.rm(rootDir, { recursive: true, force: true }));

    await fs.writeFile(path.join(rootDir, "tracked-version.txt"), "3.0.0-beta.34\n");
    let temporaryOutputDirectory;
    await assert.rejects(
        createVersionSnapshot({
            rootDir,
            runExtractor: async (outputDirectory) => {
                temporaryOutputDirectory = outputDirectory;
                const cliDirectory = path.join(temporaryOutputDirectory, "cli");
                await fs.mkdir(cliDirectory, { recursive: true });
                await fs.writeFile(
                    path.join(cliDirectory, "cli-version.json"),
                    JSON.stringify({ version: "3.0.0-beta.35+abcdef123456" }),
                );
            },
        }),
        /Missing required metadata artifact/,
    );
    assert.equal(
        await fs.readFile(path.join(rootDir, "tracked-version.txt"), "utf8"),
        "3.0.0-beta.34\n",
    );
    await assert.rejects(fs.access(temporaryOutputDirectory));
});
