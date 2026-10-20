var AdminContentJs = (function () {

    var ttimer = null;
    var askBox = null, askDone = null, askCheck = null, askBack = null;
    var dirty = false;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function checked(id) {
        var e = el(id);
        return e && e.checked ? '1' : '0';
    }

    function base() {
        return location.pathname.replace(/[^\/]*$/, '');
    }

    function getCookie(name) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + name + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function api(method, list) {
        list = list || [];
        list.push({ key: 'tok', vlu: getCookie('fs_tok') });
        $ApiRequest('AdminContent/' + method, JSON.stringify(list));
    }

    function expired() {
        document.cookie = 'fs_tok=; path=' + base() + '; max-age=0';
        location.href = 'AdminLogin';
    }

    function signout() {
        document.cookie = 'fs_tok=; path=' + base() + '; max-age=0';
        location.href = 'AdminLogin';
    }

    function toast(msg) {
        var t = el('ac-toast');
        t.textContent = msg;
        t.classList.add('ac-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ac-open');
        }, 3400);
    }

    function openMenu() {
        el('ac-rail').classList.add('ac-open');
        el('ac-scrim').classList.add('ac-open');
    }

    function closeMenu() {
        el('ac-rail').classList.remove('ac-open');
        el('ac-scrim').classList.remove('ac-open');
    }

    function openModal(title) {
        var m = el('ac-modal');
        el('ac-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('ac-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('ac-modal');
        if (m) {
            m.classList.remove('ac-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('ac-modal');
        if (!m || !m.classList.contains('ac-open')) {
            return;
        }
        if (dirty) {
            ask({ title: 'Leave without saving?', text: 'Your changes will be lost.', ok: 'Leave', no: 'Keep editing', danger: true }, shut);
            return;
        }
        shut();
    }

    function markDirty() {
        dirty = true;
    }

    function clean() {
        dirty = false;
    }

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), i: askBox.querySelector('input'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('ac-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('ac-open');
        askDone = null;
        askCheck = null;
        if (askBack && askBack.focus && document.body.contains(askBack)) {
            askBack.focus();
        }
        askBack = null;
        if (ok && f) {
            f(v);
        }
    }

    function askOk() {
        var x = askParts();
        var m = askCheck ? askCheck(x.i.value.trim()) : '';
        if (m) {
            x.e.textContent = m;
            x.i.focus();
            return;
        }
        askClose(true);
    }

    function askBuild() {
        askBox = document.createElement('div');
        askBox.className = 'ac-ask';
        askBox.innerHTML = '<div class="ac-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="ac-err" role="alert"></em><div class="ac-askacts"><button type="button" class="ac-btn ac-btn-ghost"></button><button type="button" class="ac-btn"></button></div></div>';
        document.getElementById('ac-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('ac-open')) {
                return;
            }
            if (e.key === 'Escape') {
                e.preventDefault();
                e.stopPropagation();
                askClose(false);
            } else if (e.key === 'Enter' && e.target !== x.b[0]) {
                e.preventDefault();
                e.stopPropagation();
                askOk();
            } else if (e.key === 'Tab') {
                var f = [x.i, x.b[0], x.b[1]].filter(function (n) { return n.offsetParent !== null; });
                var at = f.indexOf(document.activeElement);
                e.preventDefault();
                f[(at + (e.shiftKey ? f.length - 1 : 1)) % f.length].focus();
            }
        }, true);
    }

    function ask(o, done) {
        if (!askBox) {
            askBuild();
        }
        var x = askParts(), input = typeof o.value === 'string';
        askBack = document.activeElement;
        askDone = done;
        askCheck = o.check || null;
        x.h.textContent = o.title || '';
        x.p.textContent = o.text || '';
        x.e.textContent = '';
        x.b[0].textContent = o.no || 'Cancel';
        x.b[1].textContent = o.ok || 'OK';
        x.b[1].className = 'ac-btn' + (o.danger ? ' ac-askdel' : '');
        askBox.firstChild.classList.toggle('ac-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('ac-open');
        if (input) {
            x.i.focus();
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    function esc(s) {
        return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function collect(root, extra) {
        var list = extra || [];
        document.querySelectorAll('#' + root + ' [data-k]').forEach(function (n) {
            if (n.type === 'checkbox') {
                list.push({ key: n.getAttribute('data-k'), vlu: n.checked ? '1' : '0' });
            } else if (n.type === 'password') {
                list.push({ key: n.getAttribute('data-k'), vlu: n.value });
            } else {
                list.push({ key: n.getAttribute('data-k'), vlu: (n.value || '').trim() });
            }
        });
        return list;
    }

    var tab = 'home';
    var pickField = null, pickKind = 'photo', pickCur = '';
    var dragEl = null, dragStart = '';

    function show(name) {
        tab = name;
        api('Tab', [{ key: 'tab', vlu: name }]);
    }

    function shown(name) {
        tab = name;
        document.querySelectorAll('#ac-tabs .ac-tab').forEach(function (b) {
            b.classList.toggle('ac-act', b.getAttribute('data-t') === name);
        });
    }

    function save() {
        api('Save', collect('ac-form', [{ key: 'tab', vlu: tab }]));
        return false;
    }

    function saved() {
        toast('Saved. The website shows it now.');
    }

    function mark(id, read) {
        api('Mark', [{ key: 'id', vlu: id }, { key: 'read', vlu: read }]);
    }

    function drop(id, who) {
        ask({ title: 'Delete this message?', text: 'The message from ' + who + ' is removed for good.', ok: 'Delete message', no: 'Keep it', danger: true }, function () {
            api('Drop', [{ key: 'id', vlu: id }]);
        });
    }

    function opened(title) {
        openModal(title);
    }

    function editBlock(id, type) {
        api('EditBlock', [{ key: 'id', vlu: id || '' }, { key: 'type', vlu: type || '' }]);
    }

    function addBlock() {
        editBlock('', val('ac-addtype'));
    }

    function saveBlock(id, type) {
        var list = collect('ac-bform', [{ key: 'id', vlu: id || '' }, { key: 'type', vlu: type || '' }]);
        var side = document.querySelector('#ac-bform input[name="ac-side"]:checked');
        if (side) {
            list.push({ key: 'side', vlu: side.value });
        }
        api('SaveBlock', list);
        return false;
    }

    function blockSaved() {
        clean();
        shut();
        toast('Saved. The home page shows it now.');
    }

    function move(id, dir) {
        api('Move', [{ key: 'id', vlu: id }, { key: 'dir', vlu: dir }]);
    }

    function moved(id, dir) {
        var b = el((dir === 'up' ? 'ac-up-' : 'ac-dn-') + id);
        if (!b || b.disabled) {
            b = el((dir === 'up' ? 'ac-dn-' : 'ac-up-') + id);
        }
        if (b && b.focus) {
            b.focus();
        }
    }

    function toggle(id, to, name) {
        var showIt = to === '1';
        ask({ title: (showIt ? 'Show the ' : 'Hide the ') + name + ' block?', text: showIt ? 'It appears on the home page again.' : 'Visitors will not see it. It stays saved here and you can show it again.', ok: showIt ? 'Show block' : 'Hide block', no: 'Cancel', danger: !showIt }, function () {
            api('Toggle', [{ key: 'id', vlu: id }, { key: 'to', vlu: to }]);
        });
    }

    function dropBlock(id, name) {
        ask({ title: 'Delete this ' + name + ' block?', text: 'It is removed from the home page for good. Photos and videos stay in the library.', ok: 'Delete block', no: 'Keep it', danger: true }, function () {
            api('DropBlock', [{ key: 'id', vlu: id }]);
        });
    }

    function order() {
        var ids = [];
        document.querySelectorAll('#ac-blocks .ac-blk').forEach(function (li) {
            ids.push(li.getAttribute('data-id'));
        });
        return ids.join(',');
    }

    function bindDrag() {
        document.addEventListener('dragstart', function (e) {
            var li = e.target && e.target.closest ? e.target.closest('#ac-blocks .ac-blk') : null;
            if (!li || !li.getAttribute('draggable')) {
                return;
            }
            dragEl = li;
            dragStart = order();
            li.classList.add('ac-drag');
            e.dataTransfer.effectAllowed = 'move';
            e.dataTransfer.setData('text/plain', li.getAttribute('data-id'));
        });
        document.addEventListener('dragover', function (e) {
            if (!dragEl) {
                return;
            }
            var over = e.target && e.target.closest ? e.target.closest('#ac-blocks .ac-blk') : null;
            if (!over) {
                return;
            }
            e.preventDefault();
            e.dataTransfer.dropEffect = 'move';
            if (over === dragEl) {
                return;
            }
            var r = over.getBoundingClientRect();
            var after = e.clientY > r.top + r.height / 2;
            over.parentNode.insertBefore(dragEl, after ? over.nextSibling : over);
        });
        document.addEventListener('drop', function (e) {
            if (dragEl) {
                e.preventDefault();
            }
        });
        document.addEventListener('dragend', function () {
            if (!dragEl) {
                return;
            }
            dragEl.classList.remove('ac-drag');
            dragEl = null;
            var now = order();
            if (now !== dragStart) {
                api('Order', [{ key: 'ids', vlu: now }]);
            }
        });
    }

    function upload() {
        var msg = el('ac-pkmsg');
        if (msg) {
            msg.textContent = 'Uploading. Large videos can take a minute.';
        }
        api('Upload', [{ key: 'pk', vlu: pickKind }, { key: 'cur', vlu: pickCur }]);
    }

    function uploaded() {
        document.querySelectorAll('#ac-site-root input[type="file"]').forEach(function (f) {
            f.value = '';
        });
    }

    function preview(kind, path, thumb) {
        if (!path) {
            return '<span class="ac-mf-none">' + (kind === 'video' ? 'No video chosen' : 'No photo chosen') + '</span>';
        }
        if (kind === 'video') {
            return '<video src="' + esc(path) + '#t=0.1" muted preload="metadata" playsinline></video>';
        }
        return '<img src="' + esc(thumb || path) + '" alt="">';
    }

    function pick(field, kind) {
        pickField = field;
        pickKind = kind || 'photo';
        pickCur = val('ac-f-' + field);
        api('Pick', [{ key: 'pk', vlu: pickKind }, { key: 'cur', vlu: pickCur }]);
    }

    function picking(title) {
        var p = el('ac-pick');
        el('ac-pktitle').textContent = title;
        p.classList.add('ac-open');
        p.setAttribute('aria-hidden', 'false');
        var q = el('ac-pkq');
        if (q) {
            q.focus();
        }
    }

    function closePick() {
        var p = el('ac-pick');
        if (p) {
            p.classList.remove('ac-open');
            p.setAttribute('aria-hidden', 'true');
        }
        uploaded();
        pickField = null;
    }

    function pickOpen() {
        var p = el('ac-pick');
        return p && p.classList.contains('ac-open');
    }

    function choose(btn) {
        if (!pickField) {
            return;
        }
        var input = el('ac-f-' + pickField);
        var prev = el('ac-mfp-' + pickField);
        if (input) {
            input.value = btn.getAttribute('data-p');
        }
        if (prev) {
            prev.innerHTML = preview(btn.getAttribute('data-kind'), btn.getAttribute('data-p'), btn.getAttribute('data-t'));
        }
        var e = el('ac-e-' + pickField);
        if (e) {
            e.textContent = '';
        }
        markDirty();
        closePick();
    }

    function unpick(field, kind) {
        var input = el('ac-f-' + field);
        if (input) {
            input.value = '';
        }
        var prev = el('ac-mfp-' + field);
        if (prev) {
            prev.innerHTML = preview(kind, '', '');
        }
        markDirty();
    }

    function pkfilter() {
        var q = val('ac-pkq').toLowerCase();
        document.querySelectorAll('#ac-pkgrid .ac-pi').forEach(function (b) {
            b.hidden = q !== '' && (b.getAttribute('data-n') || '').indexOf(q) < 0;
        });
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                if (askBox && askBox.classList.contains('ac-open')) {
                    return;
                }
                if (pickOpen()) {
                    closePick();
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('ac-modal');
        if (m) {
            m.addEventListener('input', markDirty);
            m.addEventListener('change', markDirty);
            m.addEventListener('mousedown', function (e) {
                if (e.target === m) {
                    closeModal();
                }
            });
        }
        var p = el('ac-pick');
        if (p) {
            p.addEventListener('mousedown', function (e) {
                if (e.target === p) {
                    closePick();
                }
            });
        }
        bindDrag();
        api('Load');
    }

    return {
        reveal: reveal,
        expired: expired,
        signout: signout,
        toast: toast,
        openMenu: openMenu,
        closeMenu: closeMenu,
        openModal: openModal,
        closeModal: closeModal,
        shut: shut,
        clean: clean,
        show: show,
        shown: shown,
        save: save,
        saved: saved,
        mark: mark,
        drop: drop,
        opened: opened,
        editBlock: editBlock,
        addBlock: addBlock,
        saveBlock: saveBlock,
        blockSaved: blockSaved,
        move: move,
        moved: moved,
        toggle: toggle,
        dropBlock: dropBlock,
        upload: upload,
        uploaded: uploaded,
        pick: pick,
        picking: picking,
        closePick: closePick,
        choose: choose,
        unpick: unpick,
        pkfilter: pkfilter
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminContentJs.reveal);
} else {
    AdminContentJs.reveal();
}
