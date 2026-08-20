const { spawn } = require("node:child_process");
const fs = require("node:fs/promises");
const path = require("node:path");

const REQUIRED_ARTIFACTS = Object.freeze([
    "cli-version.json",
    "cli-output.json",
    "cli-namespace.json",
    "namespace-mapping.json",
]);

async function pathExists(candidatePath) {
    try {
        await fs.access(candidatePath);
        return true;
    } catch (error) {
        if (error.code === "ENOENT") {
            return false;
        }
        throw error;
    }
}

function runDotnetExtractor(temporaryOutputDirectory, rootDir) {
    const projectPath = path.resolve(rootDir, "..", "mcp-tools", "McpCliMetadata");
    const args = [
        "run",
        "--project",
        projectPath,
        "--",
        temporaryOutputDirectory,
    ];

    return new Promise((resolve, reject) => {
        const child = spawn("dotnet", args, {
            cwd: path.resolve(rootDir, ".."),
            shell: false,
            stdio: "inherit",
        });

        child.once("error", reject);
        child.once("close", (exitCode) => {
            if (exitCode === 0) {
                resolve();
                return;
            }
            reject(new Error(`McpCliMetadata exited with code ${exitCode}`));
        });
    });
}

function validateVersionDirectoryName(version) {
    if (
        typeof version !== "string"
        || !/^[0-9A-Za-z][0-9A-Za-z.+-]*$/.test(version)
        || version === "."
        || version === ".."
    ) {
        throw new Error(`CLI returned an unsafe version directory name: ${JSON.stringify(version)}`);
    }
}

async function createVersionSnapshot({
    rootDir = __dirname,
    runExtractor = (temporaryOutputDirectory) => runDotnetExtractor(
        temporaryOutputDirectory,
        rootDir,
    ),
} = {}) {
    const temporaryOutputDirectory = await fs.mkdtemp(
        path.join(path.resolve(rootDir, ".."), ".mcp-cli-snapshot-tmp-"),
    );
    const cliDirectory = path.join(temporaryOutputDirectory, "cli");

    try {
        await runExtractor(temporaryOutputDirectory);

        for (const fileName of REQUIRED_ARTIFACTS) {
            const artifactPath = path.join(cliDirectory, fileName);
            if (!await pathExists(artifactPath)) {
                throw new Error(`Missing required metadata artifact: ${artifactPath}`);
            }
        }

        const versionDocument = JSON.parse(
            await fs.readFile(path.join(cliDirectory, "cli-version.json"), "utf8"),
        );
        const { version } = versionDocument;
        validateVersionDirectoryName(version);

        const versionDirectory = path.join(rootDir, version);
        if (await pathExists(versionDirectory)) {
            throw new Error(`Version snapshot already exists: ${versionDirectory}`);
        }

        await fs.rename(cliDirectory, versionDirectory);
        const trackedVersion = version.split("+", 1)[0];
        await fs.writeFile(
            path.join(rootDir, "tracked-version.txt"),
            `${trackedVersion}\n`,
            "utf8",
        );
        return versionDirectory;
    } finally {
        await fs.rm(temporaryOutputDirectory, { recursive: true, force: true });
    }
}

async function main() {
    const versionDirectory = await createVersionSnapshot();
    console.log(`Created CLI metadata snapshot: ${versionDirectory}`);
}

if (require.main === module) {
    main().catch((error) => {
        console.error(`Failed to create CLI metadata snapshot: ${error.message}`);
        process.exitCode = 1;
    });
}

module.exports = {
    REQUIRED_ARTIFACTS,
    createVersionSnapshot,
};
