// Antigravity 2.26.0: local, reversible Russian UI overlay, v3.
// This runs in the existing isolated preload world. No IPC or network calls.
(() => {
    'use strict';
    if (window.top !== window || !['127.0.0.1', 'localhost'].includes(location.hostname)) return;
    const translations = /* RU_DICTIONARY */;
    const own = (key) => Object.prototype.hasOwnProperty.call(translations, key);
    const upperLabels = new Map(Object.entries(translations).map(([key, value]) => [key.toUpperCase(), value.toLocaleUpperCase('ru-RU')]));
    function translate(value) {
        if (typeof value !== 'string' || !value.trim()) return value;
        const key = value.trim();
        function duration(text) {
            const units = {s:'с',second:'с',seconds:'с',m:'мин',minute:'мин',minutes:'мин',h:'ч',hour:'ч',hours:'ч',d:'дн',day:'дн',days:'дн',w:'нед',week:'нед',weeks:'нед',mo:'мес',month:'мес',months:'мес',y:'г',year:'г',years:'г'};
            return text.replace(/\b(\d+)\s*(seconds?|minutes?|hours?|days?|weeks?|months?|years?|mo|[smhdwy])\b/g, (_, n, unit) => n + ' ' + units[unit]).replace(/\band\b/g, 'и');
        }
        const compactKey = key.replace(/\s+/g, ' ');
        let result = own(key) ? translations[key] : own(compactKey) ? translations[compactKey] : key;
        if (result === key && key.length < 100 && key === key.toUpperCase() && upperLabels.has(key)) result = upperLabels.get(key);
        if (result === key) {
            let match;
            if ((match = key.match(/^Select model, current: (.+)$/))) result = 'Выбрать модель, сейчас: ' + match[1].replace(/ (Low|Medium|High)$/, (_, level) => ' ' + translations[level]);
            else if ((match = key.match(/^Select project, current: (.+)$/))) result = 'Выбрать проект, сейчас: ' + (translations[match[1]] || match[1]);
            else if ((match = key.match(/^Agent permissions preset selector, current: (.+)$/))) result = 'Профиль разрешений, сейчас: ' + (translations[match[1]] || match[1]);
            else if ((match = key.match(/^No more older messages, showing (\d+) of (\d+)$/))) result = `Показаны все сообщения (${match[1]} из ${match[2]})`;
            else if ((match = key.match(/^(Select Model|Settings|Search Conversations)(Ctrl\+.+)$/))) result = (translations[match[1]] || match[1]) + ' ' + match[2];
            else if ((match = key.match(/^(\d+)(s|m|h|d|w|mo|y)( ago)?$/))) result = match[1] + ' ' + ({s:'с',m:'мин',h:'ч',d:'дн',w:'нед',mo:'мес',y:'г'}[match[2]]) + (match[3] ? ' назад' : '');
            else if ((match = key.match(/^Resets in\s+(.+)$/))) result = 'Обновление через ' + duration(match[1]);
            else if ((match = key.match(/^You have used some of your (weekly|5-hour) limit, it will fully refresh in (.+)\.$/))) result = (match[1] === 'weekly' ? 'Часть недельного лимита использована. Обновление через ' : 'Часть лимита за 5 часов использована. Обновление через ') + duration(match[2]) + '.';
            else if ((match = key.match(/^Your Plan:\s*(.+)$/))) result = 'Ваш тариф: ' + match[1];
            else if ((match = key.match(/^Send feedback as (.+)$/))) result = 'Отправить отзыв от имени ' + match[1];
            else if ((match = key.match(/^(Install|Uninstall|Enable|Disable) (.+)$/))) result = translations[match[1]] + ' ' + (translations[match[2]] || match[2]);
            else if ((match = key.match(/^e\.g[.,]?\s+(.+)$/i))) result = 'например: ' + match[1];
            else if ((match = key.match(/^More actions for (.+)$/))) result = 'Другие действия: ' + match[1];
            else if ((match = key.match(/^Settings - (.+)$/))) result = 'Настройки — ' + match[1];
            else if (/^\d+(?:mo|[smhdwy])(?:\s+\d+(?:mo|[smhdwy]))+$/.test(key)) result = duration(key);
            else if ((match = key.match(/^((?:Claude (?:Sonnet|Opus) [\d.]+|GPT-OSS [\w. -]+)) \((Thinking|Medium|High|Low)\)$/))) result = match[1] + ' (' + ({Thinking:'рассуждение',Medium:'средний',High:'высокий',Low:'низкий'}[match[2]]) + ')';
        }
        if (result === key) return value;
        return value.slice(0, value.indexOf(key)) + result + value.slice(value.indexOf(key) + key.length);
    }
    const alwaysProtected = 'script,style,pre,code,kbd,samp,textarea,input,select,option,svg,math,canvas,iframe,webview,[contenteditable]:not([contenteditable="false"]),[data-ru-preserve],.monaco-editor,.cm-editor,.ProseMirror,.tiptap,.xterm,.xterm-screen,.xterm-rows,[role="log"],.code-line,.view-lines,.cm-content';
    const messageContent = 'article,[role="article"],[data-message-id],[data-testid="message-content"]';
    const documentContent = '.markdown,.markdown-body,[class*="Markdown"]';
    const userContent = messageContent + ',' + documentContent + ',a[href*="/c/"],a[href*="/project/"],[role="treeitem"],[data-file-name],[data-resource-name]';
    function protectedElement(element, attribute = false) {
        if (!element) return true;
        // Only placeholders and accessible labels on inputs may be translated.
        if (attribute && element.matches('input,textarea,[contenteditable="true"]')) {
            return !!element.parentElement?.closest(alwaysProtected + ',' + userContent);
        }
        if (element.closest(alwaysProtected)) return true;
        if (element.closest('[role="tooltip"]') && !element.closest(messageContent)) return false;
        if (element.closest(userContent) && !element.closest('button,[role="button"]')) return true;
        return false;
    }
    function textNode(node) {
        if (!node.isConnected || protectedElement(node.parentElement)) return;
        const next = translate(node.data);
        // Set text data only: keep React's original node identity and structure.
        if (next !== node.data) node.data = next;
    }
    function attributes(element) {
        if (protectedElement(element, true)) {
            if (element.matches('article,[role="article"]')) {
                const label = element.getAttribute('aria-label');
                if (label === 'User message' || label === 'Agent response') element.setAttribute('aria-label', translate(label));
            }
            return;
        }
        for (const name of ['title', 'aria-label', 'placeholder', 'data-placeholder', 'alt']) {
            const value = element.getAttribute(name);
            if (value === null) continue;
            const next = translate(value);
            if (next !== value) element.setAttribute(name, next);
        }
    }
    function controls(root) {
        if (root.closest(alwaysProtected) || !root.matches(userContent)) return;
        for (const button of root.querySelectorAll('button,[role="button"]')) {
            if (!button.closest(alwaysProtected)) subtree(button);
        }
    }
    function subtree(root) {
        if (!root.isConnected) return;
        if (root.nodeType === Node.TEXT_NODE) { textNode(root); return; }
        if (root.nodeType !== Node.ELEMENT_NODE) return;
        attributes(root);
        if (protectedElement(root)) { controls(root); return; }
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT | NodeFilter.SHOW_TEXT, {
            acceptNode(node) {
                if (node.nodeType === Node.ELEMENT_NODE && protectedElement(node)) {
                    attributes(node);
                    // Translate app controls without walking or altering message/document text.
                    controls(node);
                    return NodeFilter.FILTER_REJECT;
                }
                return NodeFilter.FILTER_ACCEPT;
            }
        });
        for (let node = walker.nextNode(); node; node = walker.nextNode()) {
            if (node.nodeType === Node.TEXT_NODE) textNode(node);
            else attributes(node);
        }
    }
    function start() {
        if (!document.body || document.documentElement.dataset.antigravityRu === '1') return;
        document.documentElement.dataset.antigravityRu = '1';
        document.documentElement.lang = 'ru';
        const pending = new Set();
        let queued = false;
        function enqueue(node) {
            pending.add(node);
            if (queued) return;
            queued = true;
            queueMicrotask(() => {
                queued = false;
                const nodes = Array.from(pending);
                pending.clear();
                for (const root of nodes) {
                    try { subtree(root); } catch (_) { /* Translation never interrupts the app. */ }
                }
            });
        }
        const observer = new MutationObserver((records) => {
            for (const record of records) {
                if (record.type === 'childList') for (const node of record.addedNodes) enqueue(node);
                else if (record.type === 'characterData') enqueue(record.target);
                else if (record.type === 'attributes') attributes(record.target);
            }
        });
        observer.observe(document.body, {
            subtree: true, childList: true, characterData: true, attributes: true,
            attributeFilter: ['title', 'aria-label', 'placeholder', 'data-placeholder', 'alt']
        });
        subtree(document.body);
        window.addEventListener('pagehide', () => observer.disconnect(), { once: true });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start, { once: true });
    else start();
})();
