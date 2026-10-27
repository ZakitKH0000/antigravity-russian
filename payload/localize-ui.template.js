// Antigravity 2.26.0: local, reversible Russian UI overlay, v3.
// This runs in the existing isolated preload world. No IPC or network calls.
(() => {
    'use strict';
    if (window.top !== window || !['127.0.0.1', 'localhost'].includes(location.hostname)) return;
    const translations = /* RU_DICTIONARY */;
    // Labels that may be translated inside an agent response, outside its Markdown text.
    const chatLabels = new Set(/* RU_CHAT_LABELS */);
    const own = (key) => Object.prototype.hasOwnProperty.call(translations, key);
    const upperLabels = new Map(Object.entries(translations).map(([key, value]) => [key.toUpperCase(), value.toLocaleUpperCase('ru-RU')]));
    function duration(text) {
        const units = {s:'с',second:'с',seconds:'с',m:'мин',minute:'мин',minutes:'мин',h:'ч',hour:'ч',hours:'ч',d:'дн',day:'дн',days:'дн',w:'нед',week:'нед',weeks:'нед',mo:'мес',month:'мес',months:'мес',y:'г',year:'г',years:'г'};
        return text.replace(/\b(\d+)\s*(seconds?|minutes?|hours?|days?|weeks?|months?|years?|mo|[smhdwy])\b/g, (_, n, unit) => n + ' ' + units[unit]).replace(/\band\b/g, 'и');
    }
    const scopes = {'in this conversation':'в этом чате','in this workspace':'в этой рабочей области','in this project':'в этом проекте','when not in a project':'вне проектов',globally:'глобально'};
    const permissionTargets = {'read access to this path':'доступ на чтение по этому пути','write access to this path':'доступ на запись по этому пути','reading this URL':'чтение этого адреса','executing actions on this URL':'выполнение действий по этому адресу','running this command':'выполнение этой команды','running this command outside the sandbox':'выполнение этой команды вне песочницы','using this MCP tool':'использование этого инструмента MCP','a one-time administrator (UAC) elevation':'однократные права администратора (UAC)','access to this resource':'доступ к этому ресурсу'};
    const permissionActions = {read:'чтение',write:'запись',run:'выполнение','run (unsandboxed)':'выполнение вне песочницы',use:'использование','grant administrator access for':'права администратора для',access:'доступ'};
    // Generated agent step, permission and question labels; the variable part is kept as is.
    const chatPatterns = [
        [/^Thought for (\d+)s$/, (m) => 'Рассуждение: ' + m[1] + ' с'],
        [/^Thinking for (\d+)s$/, (m) => 'Рассуждение… ' + m[1] + ' с'],
        [/^Worked for (\d+[smhd](?: \d+[smhd])*)$/, (m) => 'Работа: ' + duration(m[1])],
        [/^Ran command: (.+)$/, (m) => 'Выполнена команда: ' + m[1]],
        [/^Waiting for command completion \(up to (\d+) seconds\)$/, (m) => 'Ожидание завершения команды (до ' + m[1] + ' с)'],
        [/^Exited with code (-?\d+)\.$/, (m) => 'Завершено с кодом ' + m[1] + '.'],
        [/^Question (\d+) of (\d+)$/, (m) => 'Вопрос ' + m[1] + ' из ' + m[2]],
        [/^View (\d+) side questions?$/, (m) => 'Дополнительные вопросы: ' + m[1]],
        [/^Cancel Questionnaire and Stop the Agent \((.+)\)$/, (m) => 'Отменить опрос и остановить агента (' + m[1] + ')'],
        [/^Yes, and always allow (in this project|when not in a project)$/, (m) => 'Да, и всегда разрешать ' + scopes[m[1]]],
        [/^Yes, save rule (in this project|when not in a project)$/, (m) => 'Да, сохранить правило ' + scopes[m[1]]],
        [/^Yes, and always allow '(.+)'(?: (in this conversation|in this workspace|in this project|when not in a project))?$/, (m) => 'Да, и всегда разрешать «' + m[1] + '»' + (m[2] ? ' ' + scopes[m[2]] : '')],
        [/^Yes, save rule for '(.+)' (in this conversation|in this workspace|in this project|when not in a project|globally)$/, (m) => 'Да, сохранить правило для «' + m[1] + '» ' + scopes[m[2]]],
        [/^(Allow|Save rule to always allow) (.+)\?$/, (m) => Object.prototype.hasOwnProperty.call(permissionTargets, m[2]) ? (m[1] === 'Allow' ? 'Разрешить ' : 'Сохранить правило и всегда разрешать ') + permissionTargets[m[2]] + '?' : null],
        [/^Requesting permission to (read|write|run \(unsandboxed\)|run|use|grant administrator access for|access) (.+)$/, (m) => 'Запрос разрешения: ' + permissionActions[m[1]] + ' — ' + m[2]],
        [/^Conflicts with your configured Ask permission: (.+)$/, (m) => 'Противоречит вашему правилу «Спрашивать»: ' + m[1]],
        [/^Requires manual confirmation: (.+)$/, (m) => 'Требуется подтверждение вручную: ' + m[1]],
        [/^Cost summary: (.+)$/, (m) => 'Стоимость: ' + m[1]]
    ];
    function chatPattern(key) {
        for (const [pattern, replace] of chatPatterns) {
            const match = key.match(pattern);
            if (match) return replace(match);
        }
        return null;
    }
    function replaceKey(value, key, result) {
        if (result === null || result === undefined || result === key) return value;
        return value.slice(0, value.indexOf(key)) + result + value.slice(value.indexOf(key) + key.length);
    }
    function chatLabel(value) {
        if (typeof value !== 'string' || !value.trim()) return value;
        const key = value.trim();
        return replaceKey(value, key, chatLabels.has(key) ? translations[key] : chatPattern(key));
    }
    function translate(value) {
        if (typeof value !== 'string' || !value.trim()) return value;
        const key = value.trim();
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
            else result = chatPattern(key) ?? key;
        }
        return replaceKey(value, key, result);
    }
    const alwaysProtected = 'script,style,pre,code,kbd,samp,textarea,input,select,option,svg,math,canvas,iframe,webview,[contenteditable]:not([contenteditable="false"]),[data-ru-preserve],.monaco-editor,.cm-editor,.ProseMirror,.tiptap,.xterm,.xterm-screen,.xterm-rows,[role="log"],.code-line,.view-lines,.cm-content';
    const messageContent = 'article,[role="article"],[data-message-id],[data-testid="message-content"]';
    const documentContent = '.markdown,.markdown-body,[class*="Markdown"]';
    const userContent = messageContent + ',' + documentContent + ',a[href*="/c/"],a[href*="/project/"],[role="treeitem"],[data-file-name],[data-resource-name]';
    const agentResponse = ['Agent response', translations['Agent response']].map((label) => '[role="article"][aria-label="' + label + '"]').join(',');
    // Markdown text of the response: the agent's own words stay untouched.
    const agentText = 'p,li,h1,h2,h3,h4,h5,h6,blockquote,td,th,dt,dd,a,strong,em,b,i,s,del,mark,sup,sub,figcaption,caption';
    const chatExcluded = alwaysProtected + ',' + agentText + ',' + documentContent + ',[role="treeitem"],[data-file-name],[data-resource-name]';
    function chatChrome(element) {
        return !!element && !!element.closest(agentResponse) && !element.closest(chatExcluded);
    }
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
        if (!node.isConnected) return;
        const parent = node.parentElement;
        const next = !protectedElement(parent) ? translate(node.data) : chatChrome(parent) ? chatLabel(node.data) : node.data;
        // Set text data only: keep React's original node identity and structure.
        if (next !== node.data) node.data = next;
    }
    function attributes(element) {
        if (protectedElement(element, true)) {
            if (element.matches('article,[role="article"]')) {
                const label = element.getAttribute('aria-label');
                if (label === 'User message' || label === 'Agent response') element.setAttribute('aria-label', translate(label));
            }
            if (chatChrome(element)) labelAttributes(element, chatLabel);
            return;
        }
        labelAttributes(element, translate);
    }
    function labelAttributes(element, convert) {
        for (const name of ['title', 'aria-label', 'placeholder', 'data-placeholder', 'alt']) {
            const value = element.getAttribute(name);
            if (value === null) continue;
            const next = convert(value);
            if (next !== value) element.setAttribute(name, next);
        }
    }
    function controls(root) {
        if (root.closest(alwaysProtected) || !root.matches(userContent)) return;
        for (const button of root.querySelectorAll('button,[role="button"]')) {
            if (!button.closest(alwaysProtected)) subtree(button);
        }
    }
    function chat(root) {
        // Step titles, permission and question cards of an agent response; its Markdown text is skipped.
        if (!chatChrome(root)) return;
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT | NodeFilter.SHOW_TEXT, {
            acceptNode(node) {
                return node.nodeType === Node.ELEMENT_NODE && !chatChrome(node) ? NodeFilter.FILTER_REJECT : NodeFilter.FILTER_ACCEPT;
            }
        });
        for (let node = walker.nextNode(); node; node = walker.nextNode()) {
            if (node.nodeType === Node.TEXT_NODE) textNode(node);
            else attributes(node);
        }
    }
    function subtree(root) {
        if (!root.isConnected) return;
        if (root.nodeType === Node.TEXT_NODE) { textNode(root); return; }
        if (root.nodeType !== Node.ELEMENT_NODE) return;
        attributes(root);
        if (protectedElement(root)) { controls(root); chat(root); return; }
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT | NodeFilter.SHOW_TEXT, {
            acceptNode(node) {
                if (node.nodeType === Node.ELEMENT_NODE && protectedElement(node)) {
                    attributes(node);
                    // Translate app controls without walking or altering message/document text.
                    controls(node);
                    chat(node);
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
