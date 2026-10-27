// DOM checks of payload/localize-ui.js. Optional: needs Node.js and jsdom, not used by Build.ps1.
// Run after Sync-Payload.ps1: set NODE_PATH to a folder with jsdom, then: node tests/overlay/overlay.test.cjs
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { JSDOM } = require('jsdom');
const payload = fs.readFileSync(path.join(__dirname, '..', '..', 'payload', 'localize-ui.js'), 'utf8');
const delay = () => new Promise((resolve) => setTimeout(resolve, 20));

async function open(html, url = 'https://127.0.0.1:12345/') {
    const dom = new JSDOM('<body>' + html + '</body>', { url, runScripts: 'outside-only' });
    dom.window.eval(payload);
    dom.window.document.dispatchEvent(new dom.window.Event('DOMContentLoaded'));
    await delay();
    return dom;
}
const text = (d, selector) => d.querySelector(selector).textContent;

async function interfaceAndUserContent() {
    const dom = await open(`
        <nav><button id="settings">Settings</button><a id="chat-title" href="/c/abc">Settings</a></nav>
        <div id="react">New Conversation</div><input id="input" placeholder="Ask anything..." value="Settings">
        <div id="editor" contenteditable="true">New Conversation</div>
        <pre id="code">const label = 'Settings';</pre><div class="markdown-body" id="doc">Settings</div>
        <div role="treeitem" id="file">Settings</div><div class="xterm" id="terminal">Error</div>`);
    const d = dom.window.document;
    assert.equal(d.documentElement.lang, 'ru');
    assert.equal(text(d, '#settings'), 'Настройки');
    assert.equal(text(d, '#react'), 'Новый чат');
    assert.equal(d.querySelector('#input').placeholder, 'Задайте вопрос…');
    assert.equal(d.querySelector('#input').value, 'Settings');
    for (const id of ['#editor', '#code', '#doc', '#chat-title', '#file', '#terminal']) assert.doesNotMatch(text(d, id), /[а-я]/i, id);
    const reactNode = d.querySelector('#react').firstChild;
    reactNode.data = 'Conversation History'; await delay();
    assert.equal(d.querySelector('#react').firstChild, reactNode);
    assert.equal(reactNode.data, 'История чатов');
    dom.window.close();
}

async function agentSteps() {
    const dom = await open(`
        <div role="article" aria-label="User message"><p>Edited</p><span id="user-span">Thought for 3s</span></div>
        <div role="article" aria-label="Agent response" data-quotable="true">
          <div class="flex"><span id="thought" aria-label="Thought for 12s">Thought for 12s</span></div>
          <div class="flex"><span id="prefix" class="text-muted-foreground">Edited</span><span id="file" title="main.ts">main.ts</span></div>
          <div class="flex"><span id="analyzed">Analyzed</span><span data-file-name="Edited">Edited</span></div>
          <div id="worked">Worked for 2h 5m</div>
          <div id="waiting">Waiting for user input</div>
          <div id="command">Ran command: npm run build</div>
          <p id="prose">Edited</p><ul><li id="item">Ran command</li></ul><h3 id="heading">Analyzed</h3>
          <div id="free">I edited the file and analyzed the logs.</div>
          <div id="generic">Settings</div>
          <pre><code id="code">Edited</code></pre>
          <button id="copy" aria-label="Copy">Copy</button>
        </div>`);
    const d = dom.window.document;
    assert.equal(text(d, '#thought'), 'Рассуждение: 12 с');
    assert.equal(d.querySelector('#thought').getAttribute('aria-label'), 'Рассуждение: 12 с');
    assert.equal(text(d, '#prefix'), 'Изменено');
    assert.equal(text(d, '#file'), 'main.ts');
    assert.equal(text(d, '#analyzed'), 'Просмотрено');
    assert.equal(d.querySelector('[data-file-name]').textContent, 'Edited');
    assert.equal(text(d, '#worked'), 'Работа: 2 ч 5 мин');
    assert.equal(text(d, '#waiting'), 'Ожидание вашего ответа');
    assert.equal(text(d, '#command'), 'Выполнена команда: npm run build');
    // The agent's Markdown text, code and generic words are not chat labels.
    assert.equal(text(d, '#prose'), 'Edited');
    assert.equal(text(d, '#item'), 'Ran command');
    assert.equal(text(d, '#heading'), 'Analyzed');
    assert.equal(text(d, '#free'), 'I edited the file and analyzed the logs.');
    assert.equal(text(d, '#generic'), 'Settings');
    assert.equal(text(d, '#code'), 'Edited');
    assert.equal(text(d, '#copy'), 'Копировать');
    // The user's own message stays exactly as written.
    assert.equal(d.querySelector('[aria-label="Сообщение пользователя"] p').textContent, 'Edited');
    assert.equal(text(d, '#user-span'), 'Thought for 3s');
    // Steps appended or updated later while the agent is running.
    const response = d.querySelector('[aria-label="Ответ агента"]');
    const row = d.createElement('div');
    row.innerHTML = '<span id="late">Running command</span><span>npm test</span>';
    response.append(row); await delay();
    assert.equal(text(d, '#late'), 'Выполняется команда');
    const prefix = d.querySelector('#prefix').firstChild;
    prefix.data = 'Creating'; await delay();
    assert.equal(d.querySelector('#prefix').firstChild, prefix);
    assert.equal(prefix.data, 'Создание');
    // Summaries seen in a live 2.26.0 chat: group titles, counters and server suffixes.
    const live = d.createElement('div');
    live.innerHTML = '<span id="group">Explored file, ran commands</span><span id="group2">Ran command, edited files</span>' +
        '<span id="asking">Asking</span><span id="count">1 question</span><span id="count5">5 questions</span>' +
        '<span id="changed">2 files changed</span><span id="changed1">1 file changed</span><span id="working">Working</span>' +
        '<span id="exit">search root for Prishchepka folder · Exited with code 1</span>' +
        '<span id="finished">Search root for Prishchepka folder finished</span><span id="summary">search for prishchepka folder</span>' +
        '<span id="odd">Explored the repository layout</span>';
    response.append(live); await delay();
    assert.equal(text(d, '#group'), 'Изучен файл, выполнены команды');
    assert.equal(text(d, '#group2'), 'Выполнена команда, изменены файлы');
    assert.equal(text(d, '#asking'), 'Агент задаёт');
    assert.equal(text(d, '#count'), '1 вопрос');
    assert.equal(text(d, '#count5'), '5 вопросов');
    assert.equal(text(d, '#changed'), 'Изменено 2 файла');
    assert.equal(text(d, '#changed1'), 'Изменён 1 файл');
    assert.equal(text(d, '#working'), 'Выполняется');
    assert.equal(text(d, '#exit'), 'search root for Prishchepka folder · код завершения 1');
    assert.equal(text(d, '#finished'), 'Search root for Prishchepka folder — завершено');
    assert.equal(text(d, '#summary'), 'search for prishchepka folder');
    assert.equal(text(d, '#odd'), 'Explored the repository layout');
    const paragraph = d.createElement('p');
    paragraph.textContent = 'Created';
    response.append(paragraph); await delay();
    assert.equal(paragraph.textContent, 'Created');
    dom.window.close();
}

async function permissionsAndQuestions() {
    const dom = await open(`
        <div role="article" aria-label="Agent response">
          <div id="question">Allow running this command?</div>
          <div id="model-question">Allow me to delete the build folder?</div>
          <div id="once">Yes, allow this time</div>
          <div id="conversation">Yes, and always allow in this conversation</div>
          <div id="project">Yes, and always allow in this project</div>
          <div id="named">Yes, and always allow 'npm' in this workspace</div>
          <div id="rule">Yes, save rule for 'git status' globally</div>
          <div id="global">Yes, save rule globally</div>
          <div id="manual">Requires manual confirmation: deny list entry</div>
          <span id="counter" aria-label="Question 2 of 3">2/3</span>
        </div>
        <div id="notification-title">The agent has a question for you:</div>
        <div id="notification-body">Click to respond to the questions.</div>
        <div id="request">Requesting permission to read C:\\project\\a.txt</div>`);
    const d = dom.window.document;
    assert.equal(text(d, '#question'), 'Разрешить выполнение этой команды?');
    assert.equal(text(d, '#model-question'), 'Allow me to delete the build folder?');
    assert.equal(text(d, '#once'), 'Да, разрешить в этот раз');
    assert.equal(text(d, '#conversation'), 'Да, и всегда разрешать в этом чате');
    assert.equal(text(d, '#project'), 'Да, и всегда разрешать в этом проекте');
    assert.equal(text(d, '#named'), 'Да, и всегда разрешать «npm» в этой рабочей области');
    assert.equal(text(d, '#rule'), 'Да, сохранить правило для «git status» глобально');
    assert.equal(text(d, '#global'), 'Да, сохранить правило глобально');
    assert.equal(text(d, '#manual'), 'Требуется подтверждение вручную: deny list entry');
    assert.equal(d.querySelector('#counter').getAttribute('aria-label'), 'Вопрос 2 из 3');
    assert.equal(text(d, '#notification-title'), 'У агента есть вопрос:');
    assert.equal(text(d, '#notification-body'), 'Нажмите, чтобы ответить на вопросы.');
    assert.equal(text(d, '#request'), 'Запрос разрешения: чтение — C:\\project\\a.txt');
    dom.window.close();
}

async function externalOrigin() {
    const dom = await open('<button>Settings</button>', 'https://example.com/');
    assert.equal(dom.window.document.querySelector('button').textContent, 'Settings');
    dom.window.close();
}

(async () => {
    for (const test of [interfaceAndUserContent, agentSteps, permissionsAndQuestions, externalOrigin]) {
        await test();
        console.log('PASS ' + test.name);
    }
})().catch((error) => { console.error(error); process.exit(1); });
