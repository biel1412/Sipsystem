// Funções do navegador usadas pelo SIP (chamadas a partir do C# via IJSRuntime).
window.sip = (function () {
    const ouvintes = {};

    function baixar(nome, tipo, base64) {
        const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
        const url = URL.createObjectURL(new Blob([bytes], { type: tipo }));
        const a = document.createElement("a");
        a.href = url;
        a.download = nome;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    }

    function imprimir() { window.print(); }

    async function copiar(texto) {
        try { await navigator.clipboard.writeText(texto); return true; }
        catch { return false; }
    }

    function lerComoBase64(arquivo) {
        return new Promise((resolve, reject) => {
            const leitor = new FileReader();
            leitor.onload = () => resolve(String(leitor.result).split(",")[1]);
            leitor.onerror = reject;
            leitor.readAsDataURL(arquivo);
        });
    }

    // Ctrl+V de um print (Win+Shift+S / PrtSc) em qualquer lugar da página: manda a imagem para o C#.
    // Texto colado em campos continua funcionando normalmente (só imagens são capturadas).
    function escutarColar(id, dotnet) {
        pararColar(id);
        const el = document;
        const ao = async (e) => {
            const itens = Array.from(e.clipboardData?.items || []).filter(i => i.kind === "file" && i.type.startsWith("image/"));
            if (itens.length === 0) return;
            e.preventDefault();
            for (const item of itens) {
                const arquivo = item.getAsFile();
                if (!arquivo) continue;
                const base64 = await lerComoBase64(arquivo);
                const nome = arquivo.name && arquivo.name !== "image.png"
                    ? arquivo.name
                    : `print_colado_${new Date().toISOString().slice(11, 19).replaceAll(":", "")}.png`;
                await dotnet.invokeMethodAsync("ImagemColada", nome, arquivo.type, arquivo.size, base64);
            }
        };
        el.addEventListener("paste", ao);
        ouvintes[id] = { el, ao };
    }

    function pararColar(id) {
        const o = ouvintes[id];
        if (o) { o.el.removeEventListener("paste", o.ao); delete ouvintes[id]; }
    }

    return { baixar, imprimir, copiar, escutarColar, pararColar };
})();
