var AdminProductsJs = (function () {

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
        $ApiRequest('AdminProducts/' + method, JSON.stringify(list));
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
        var t = el('ap-toast');
        t.textContent = msg;
        t.classList.add('ap-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ap-open');
        }, 3400);
    }

    function openMenu() {
        el('ap-rail').classList.add('ap-open');
        el('ap-scrim').classList.add('ap-open');
    }

    function closeMenu() {
        el('ap-rail').classList.remove('ap-open');
        el('ap-scrim').classList.remove('ap-open');
    }

    function openModal(title) {
        var m = el('ap-modal');
        el('ap-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('ap-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('ap-modal');
        if (m) {
            m.classList.remove('ap-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('ap-modal');
        if (!m || !m.classList.contains('ap-open')) {
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
        if (!askBox || !askBox.classList.contains('ap-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('ap-open');
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
        askBox.className = 'ap-ask';
        askBox.innerHTML = '<div class="ap-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="ap-err" role="alert"></em><div class="ap-askacts"><button type="button" class="ap-btn ap-btn-ghost"></button><button type="button" class="ap-btn"></button></div></div>';
        document.getElementById('ap-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('ap-open')) {
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
        x.b[1].className = 'ap-btn' + (o.danger ? ' ap-askdel' : '');
        askBox.firstChild.classList.toggle('ap-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('ap-open');
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

    var timer = null;
    var pageNo = 1;

    function filters(page) {
        return [
            { key: 'term', vlu: val('ap-term') },
            { key: 'st', vlu: val('ap-st') },
            { key: 'occ', vlu: val('ap-occ') },
            { key: 'pg', vlu: String(page || 1) }
        ];
    }

    function list(page) {
        pageNo = page || 1;
        api('List', filters(pageNo));
    }

    function type() {
        clearTimeout(timer);
        timer = setTimeout(function () {
            list(1);
        }, 250);
    }

    function edit(id) {
        api('Edit', [{ key: 'id', vlu: id || '' }]);
    }

    function opened(title) {
        openModal(title);
    }

    var photos = [];
    var picked = [];
    var maxPhotos = 6;

    function readPhotos() {
        photos = [];
        document.querySelectorAll('#ap-photos .ap-pic').forEach(function (f) {
            photos.push({ p: f.getAttribute('data-p'), t: f.getAttribute('data-t'), k: f.getAttribute('data-kind') || 'photo' });
        });
    }

    function paintPhotos() {
        var box = el('ap-photos');
        if (!box) {
            return;
        }
        var h = '';
        photos.forEach(function (x, i) {
            var pv = x.k === 'video' ? '<video src="' + esc(x.p) + '#t=0.1" muted preload="metadata" playsinline></video><i class="ap-pi-play" aria-hidden="true"><svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor"><path d="M8 5v14l11-7z"/></svg></i>' : '<img src="' + esc(x.t) + '" alt="">';
            h += '<figure class="ap-pic" data-p="' + esc(x.p) + '" data-t="' + esc(x.t) + '" data-kind="' + esc(x.k) + '">' + pv + (i === 0 ? '<figcaption>Main</figcaption>' : '') +
                '<div class="ap-pic-b"><button type="button" aria-label="Move left" onclick="AdminProductsJs.shift(' + i + ',-1)"' + (i === 0 ? ' disabled' : '') + '>&lsaquo;</button>' +
                '<button type="button" aria-label="Move right" onclick="AdminProductsJs.shift(' + i + ',1)"' + (i === photos.length - 1 ? ' disabled' : '') + '>&rsaquo;</button>' +
                '<button type="button" aria-label="Remove" onclick="AdminProductsJs.unpick(' + i + ')">&times;</button></div></figure>';
        });
        box.innerHTML = h || '<span class="ap-pic-none">Nothing chosen yet.</span>';
        el('ap-f-photos').value = photos.map(function (x) { return x.p; }).join('|');
        var e = el('ap-e-photos');
        if (e) {
            e.textContent = '';
        }
    }

    function shift(i, d) {
        readPhotos();
        var j = i + d;
        if (j < 0 || j >= photos.length) {
            return;
        }
        var t = photos[i];
        photos[i] = photos[j];
        photos[j] = t;
        paintPhotos();
        markDirty();
    }

    function unpick(i) {
        readPhotos();
        photos.splice(i, 1);
        paintPhotos();
        markDirty();
    }

    var single = '';

    function pickPhotos() {
        single = '';
        readPhotos();
        picked = photos.slice();
        api('Pick', []);
    }

    function pickOne(field) {
        single = field;
        picked = [];
        api('Pick', []);
    }

    function tab(name) {
        api('Tab', [{ key: 'tab', vlu: name }]);
    }

    function saveOccasions() {
        api('SaveOccasions', collect('ap-oform'));
        return false;
    }

    function marks() {
        document.querySelectorAll('#ap-pkgrid .ap-pi').forEach(function (b) {
            var at = -1;
            picked.forEach(function (x, i) {
                if (x.p === b.getAttribute('data-p')) {
                    at = i;
                }
            });
            b.classList.toggle('ap-pi-on', at >= 0);
            b.setAttribute('aria-pressed', at >= 0 ? 'true' : 'false');
            b.querySelector('.ap-pi-n').textContent = at >= 0 ? String(at + 1) : '';
        });
        var c = el('ap-pkcount');
        if (c) {
            c.textContent = picked.length + ' of ' + maxPhotos + ' chosen';
        }
    }

    function picking() {
        var p = el('ap-pick');
        p.classList.add('ap-open');
        p.setAttribute('aria-hidden', 'false');
        var acts = document.querySelector('#ap-pkbody .ap-pkacts');
        if (acts) {
            acts.hidden = !!single;
        }
        marks();
        var q = el('ap-pkq');
        if (q) {
            q.focus();
        }
    }

    function pickOpen() {
        var p = el('ap-pick');
        return p && p.classList.contains('ap-open');
    }

    function closePick() {
        var p = el('ap-pick');
        if (p) {
            p.classList.remove('ap-open');
            p.setAttribute('aria-hidden', 'true');
        }
        uploaded();
    }

    function choose(btn) {
        var path = btn.getAttribute('data-p');
        if (single) {
            if ((btn.getAttribute('data-kind') || 'photo') !== 'photo') {
                toast('Choose a photo here.');
                return;
            }
            el('ap-f-' + single).value = path;
            var pv = el('ap-opic-' + single.replace('oi', ''));
            if (pv) {
                pv.innerHTML = '<img src="' + esc(btn.getAttribute('data-t')) + '" alt="">';
            }
            var er = el('ap-e-' + single);
            if (er) {
                er.textContent = '';
            }
            closePick();
            return;
        }
        var at = -1;
        picked.forEach(function (x, i) {
            if (x.p === path) {
                at = i;
            }
        });
        if (at >= 0) {
            picked.splice(at, 1);
        } else if (picked.length >= maxPhotos) {
            toast('Up to ' + maxPhotos + ' items. Remove one first.');
            return;
        } else {
            picked.push({ p: path, t: btn.getAttribute('data-t'), k: btn.getAttribute('data-kind') || 'photo' });
        }
        marks();
    }

    function usePicked() {
        if (picked.length && picked[0].k === 'video') {
            toast('The first item must be a photo. Pick a photo first, then the video.');
            return;
        }
        photos = picked.slice();
        paintPhotos();
        markDirty();
        closePick();
    }

    function pkfilter() {
        var q = val('ap-pkq').toLowerCase();
        document.querySelectorAll('#ap-pkgrid .ap-pi').forEach(function (b) {
            b.hidden = q !== '' && (b.getAttribute('data-n') || '').indexOf(q) < 0;
        });
    }

    function upload() {
        var m = el('ap-pkmsg');
        if (m) {
            m.textContent = 'Uploading. Large videos can take a minute.';
        }
        api('Upload', []);
    }

    function uploaded() {
        document.querySelectorAll('#ap-site-root input[type="file"]').forEach(function (f) {
            f.value = '';
        });
        if (pickOpen()) {
            marks();
        }
    }

    function save(id) {
        var sizes = [];
        document.querySelectorAll('#ap-form .ap-sizerow').forEach(function (r) {
            var inputs = r.querySelectorAll('input');
            sizes.push({ l: inputs[0].value.trim(), p: inputs[1].value.trim() });
        });
        var occ = [];
        document.querySelectorAll('#ap-form input[name="ap-occ"]:checked').forEach(function (c) {
            occ.push(c.value);
        });
        var list2 = collect('ap-form', [{ key: 'id', vlu: id || '' }, { key: 'sizes', vlu: JSON.stringify(sizes) }, { key: 'occs', vlu: occ.join(',') }]);
        list2.push.apply(list2, filters(pageNo));
        api('Save', list2);
        return false;
    }

    function saved() {
        clean();
        shut();
        toast('Bouquet saved');
    }

    function publish(id, to, title) {
        ask({ title: to === '1' ? 'Publish ' + title + '?' : 'Hide ' + title + '?', text: to === '1' ? 'It appears in the shop right away.' : 'Customers will no longer see it. Existing carts show it as unavailable.', ok: to === '1' ? 'Publish' : 'Hide', no: 'Cancel', danger: to !== '1' }, function () {
            var l = filters(pageNo);
            l.push({ key: 'id', vlu: id }, { key: 'to', vlu: to });
            api('Publish', l);
        });
    }

    function remove(id, title) {
        ask({ title: 'Delete ' + title + '?', text: 'This removes the bouquet from the shop for good. Past orders keep their details.', ok: 'Delete bouquet', no: 'Keep it', danger: true }, function () {
            var l = filters(pageNo);
            l.push({ key: 'id', vlu: id });
            api('Remove', l);
        });
    }

    function done(msg) {
        toast(msg);
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                if (askBox && askBox.classList.contains('ap-open')) {
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
        var m = el('ap-modal');
        if (m) {
            m.addEventListener('input', markDirty);
            m.addEventListener('change', markDirty);
            m.addEventListener('mousedown', function (e) {
                if (e.target === m) {
                    closeModal();
                }
            });
        }
        var p = el('ap-pick');
        if (p) {
            p.addEventListener('mousedown', function (e) {
                if (e.target === p) {
                    closePick();
                }
            });
        }
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
        list: list,
        type: type,
        edit: edit,
        opened: opened,
        shift: shift,
        unpick: unpick,
        pickPhotos: pickPhotos,
        pickOne: pickOne,
        tab: tab,
        saveOccasions: saveOccasions,
        picking: picking,
        closePick: closePick,
        choose: choose,
        usePicked: usePicked,
        pkfilter: pkfilter,
        upload: upload,
        uploaded: uploaded,
        save: save,
        saved: saved,
        publish: publish,
        remove: remove,
        done: done
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminProductsJs.reveal);
} else {
    AdminProductsJs.reveal();
}
