window.mcpServerAppShell = (() => {
    let workspaceShortcutHandler = null;

    function unregisterWorkspaceShortcut() {
        if (workspaceShortcutHandler) {
            document.removeEventListener("keydown", workspaceShortcutHandler, true);
            workspaceShortcutHandler = null;
        }
    }

    return {
        registerWorkspaceShortcut(dotNetReference) {
            unregisterWorkspaceShortcut();
            workspaceShortcutHandler = event => {
                if (!event.ctrlKey || event.shiftKey || event.altKey) {
                    return;
                }

                if ((event.key || "").toLowerCase() !== "w") {
                    return;
                }

                event.preventDefault();
                dotNetReference.invokeMethodAsync("OpenWorkspacePickerFromShortcut");
            };
            document.addEventListener("keydown", workspaceShortcutHandler, true);
        },

        unregisterWorkspaceShortcut,

        goBack() {
            window.history.back();
        }
    };
})();
