(function () {
    const storageKey = "request-tracker.agent-chat-flyout.width";
    let activeResize = null;

    function clamp(value, min, max) {
        return Math.min(max, Math.max(min, value));
    }

    function stopResize() {
        if (!activeResize) {
            return;
        }

        window.removeEventListener("mousemove", activeResize.onMove, true);
        window.removeEventListener("mouseup", activeResize.onUp, true);
        document.body.style.userSelect = activeResize.previousUserSelect;
        document.body.style.cursor = activeResize.previousCursor;
        activeResize = null;
    }

    window.agentChatFlyout = {
        beginResize: function (panelElement, dotNetHelper, startClientX, startWidth, minWidth, maxWidth) {
            stopResize();

            if (!panelElement) {
                return;
            }

            const previousUserSelect = document.body.style.userSelect;
            const previousCursor = document.body.style.cursor;
            document.body.style.userSelect = "none";
            document.body.style.cursor = "ew-resize";

            const updatePanelWidth = function (width) {
                panelElement.style.width = "min(" + width + "px, 100vw)";
            };

            const resolveWidth = function (clientX) {
                const viewportMaxWidth = Math.max(minWidth, window.innerWidth);
                const constrainedMaxWidth = Math.max(minWidth, Math.min(maxWidth, viewportMaxWidth));
                return clamp(startWidth + (startClientX - clientX), minWidth, constrainedMaxWidth);
            };

            const onMove = function (event) {
                updatePanelWidth(resolveWidth(event.clientX));
            };

            const onUp = function (event) {
                const finalWidth = resolveWidth(event.clientX);

                stopResize();
                updatePanelWidth(finalWidth);
                dotNetHelper.invokeMethodAsync("CompleteResizeAsync", finalWidth);
            };

            activeResize = {
                onMove: onMove,
                onUp: onUp,
                previousUserSelect: previousUserSelect,
                previousCursor: previousCursor
            };

            window.addEventListener("mousemove", onMove, true);
            window.addEventListener("mouseup", onUp, true);
        },

        getStoredWidth: function () {
            const storedValue = window.localStorage.getItem(storageKey);
            if (!storedValue) {
                return "";
            }

            const parsedValue = Number.parseFloat(storedValue);
            return Number.isFinite(parsedValue) ? String(parsedValue) : "";
        },

        storeWidth: function (width) {
            window.localStorage.setItem(storageKey, String(width));
        },

        cancelResize: function () {
            stopResize();
        }
    };
})();
