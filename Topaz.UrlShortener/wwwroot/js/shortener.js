"use strict";

const form = document.getElementById("shorten-form");
const originalUrlInput = document.getElementById("original-url");
const aliasInput = document.getElementById("alias");
const generateButton = document.getElementById("generate-button");
const message = document.getElementById("message");
const result = document.getElementById("result");
const shortUrlLink = document.getElementById("short-url");
const copyButton = document.getElementById("copy-button");
const copyMessage = document.getElementById("copy-message");

let isSubmitting = false;

form.addEventListener("submit", async (event) => {
    event.preventDefault();

    if (isSubmitting) {
        return;
    }

    message.hidden = true;
    message.textContent = "";
    result.hidden = true;
    shortUrlLink.removeAttribute("href");
    shortUrlLink.textContent = "";
    copyMessage.textContent = "";

    const request = {
        originalUrl: originalUrlInput.value.trim(),
        alias: aliasInput.value.trim() || null
    };

    isSubmitting = true;
    generateButton.disabled = true;
    generateButton.textContent = "Gerando...";
    form.setAttribute("aria-busy", "true");

    try {
        const response = await fetch(form.dataset.apiUrl, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "Accept": "application/json"
            },
            body: JSON.stringify(request)
        });

        // Uma falha no servidor pode retornar uma resposta sem JSON.
        const data = await response.json().catch(() => null);

        if (!response.ok) {
            const errorMessage =
                data?.detail ||
                data?.title ||
                `Não foi possível gerar o link (HTTP ${response.status}).`;

            message.textContent = errorMessage;
            message.hidden = false;
            return;
        }

        if (!data || typeof data.shortUrl !== "string") {
            message.textContent = "A API retornou uma resposta inesperada.";
            message.hidden = false;
            return;
        }

        const shortUrl = new URL(data.shortUrl, window.location.origin);

        if (shortUrl.protocol !== "http:" &&
            shortUrl.protocol !== "https:") {
            message.textContent = "A API retornou um link inválido.";
            message.hidden = false;
            return;
        }

        shortUrlLink.href = shortUrl.href;
        shortUrlLink.textContent = shortUrl.href;
        result.hidden = false;
    } catch {
        message.textContent =
            "Não foi possível conectar à API. Tente novamente.";
        message.hidden = false;
    } finally {
        isSubmitting = false;
        generateButton.disabled = false;
        generateButton.textContent = "Gerar link curto";
        form.removeAttribute("aria-busy");
    }
});

copyButton.addEventListener("click", async () => {
    const shortUrl = shortUrlLink.textContent;

    if (!shortUrl) {
        return;
    }

    try {
        await navigator.clipboard.writeText(shortUrl);
        copyMessage.textContent = "Link copiado!";
    } catch {
        copyMessage.textContent =
            "Não foi possível copiar automaticamente. Selecione o link e copie manualmente.";
    }
});