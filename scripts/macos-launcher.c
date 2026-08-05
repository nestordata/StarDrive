/* Minimal CFBundleExecutable for StarDrive.app.
 * macOS codesign treats .NET PE .dll files as nested code, so the self-contained
 * publish cannot live in Contents/MacOS. This stub only chdirs + execs into
 * Contents/Resources/game/StarDrive (real apphost + managed runtime).
 */
#include <limits.h>
#include <mach-o/dyld.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

int main(int argc, char** argv)
{
    char exe[PATH_MAX];
    uint32_t size = sizeof(exe);
    if (_NSGetExecutablePath(exe, &size) != 0)
    {
        fprintf(stderr, "StarDrive launcher: cannot resolve executable path\n");
        return 127;
    }

    char resolved[PATH_MAX];
    if (!realpath(exe, resolved))
        strncpy(resolved, exe, sizeof(resolved) - 1);

    // .../StarDrive.app/Contents/MacOS/StarDrive -> .../Contents/Resources/game
    char* slash = strrchr(resolved, '/');
    if (!slash)
        return 127;
    *slash = '\0'; // MacOS
    slash = strrchr(resolved, '/');
    if (!slash)
        return 127;
    *slash = '\0'; // Contents

    char gameDir[PATH_MAX];
    char gameBin[PATH_MAX];
    snprintf(gameDir, sizeof(gameDir), "%s/Resources/game", resolved);
    snprintf(gameBin, sizeof(gameBin), "%s/StarDrive", gameDir);

    if (chdir(gameDir) != 0)
    {
        perror("StarDrive launcher: chdir Resources/game");
        return 127;
    }

    /* Rebuild argv[0] to the real apphost for nicer diagnostics */
    argv[0] = gameBin;
    execv(gameBin, argv);
    perror("StarDrive launcher: execv Resources/game/StarDrive");
    return 127;
}
