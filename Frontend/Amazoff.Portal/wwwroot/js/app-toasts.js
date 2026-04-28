(() => {
    const containerId = "amazoff-toast-stack";

    function getContainer() {
        let container = document.getElementById(containerId);

        if (container) {
            return container;
        }

        container = document.createElement("div");
        container.id = containerId;
        container.className = "amazoff-toast-stack";
        document.body.appendChild(container);
        return container;
    }

    function show(message, kind) {
        const container = getContainer();
        const toast = document.createElement("div");
        toast.className = `amazoff-toast amazoff-toast--${kind}`;

        const text = document.createElement("span");
        text.className = "amazoff-toast__message";
        text.textContent = message;

        const close = document.createElement("button");
        close.type = "button";
        close.className = "amazoff-toast__close";
        close.setAttribute("aria-label", "Fechar");
        close.textContent = "x";

        const removeToast = () => {
            toast.classList.add("amazoff-toast--closing");
            window.setTimeout(() => toast.remove(), 180);
        };

        close.addEventListener("click", removeToast);
        toast.append(text, close);
        container.appendChild(toast);

        window.setTimeout(removeToast, 3500);
    }

    window.amazoffToast = {
        success(message) {
            show(message, "success");
        },
        error(message) {
            show(message, "error");
        }
    };
})();
