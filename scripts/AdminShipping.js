var AdminShippingJs = (function () {

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
        $ApiRequest('AdminShipping/' + method, JSON.stringify(list));
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
        var t = el('sp-toast');
        t.textContent = msg;
        t.classList.add('sp-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('sp-open');
        }, 3400);
    }

    function openMenu() {
        el('sp-rail').classList.add('sp-open');
        el('sp-scrim').classList.add('sp-open');
    }

    function closeMenu() {
        el('sp-rail').classList.remove('sp-open');
        el('sp-scrim').classList.remove('sp-open');
    }

    function openModal(title) {
        var m = el('sp-modal');
        el('sp-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('sp-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('sp-modal');
        if (m) {
            m.classList.remove('sp-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('sp-modal');
        if (!m || !m.classList.contains('sp-open')) {
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
        if (!askBox || !askBox.classList.contains('sp-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('sp-open');
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
        askBox.className = 'sp-ask';
        askBox.innerHTML = '<div class="sp-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="sp-err" role="alert"></em><div class="sp-askacts"><button type="button" class="sp-btn sp-btn-ghost"></button><button type="button" class="sp-btn"></button></div></div>';
        document.getElementById('sp-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('sp-open')) {
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
        x.b[1].className = 'sp-btn' + (o.danger ? ' sp-askdel' : '');
        askBox.firstChild.classList.toggle('sp-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('sp-open');
        if (input) {
            x.i.focus();
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    function esc(s) {
        return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function board() {
        api('Board', [{ key: 'day', vlu: val('sp-day') }, { key: 'way', vlu: val('sp-way') }]);
    }

    function shift(d) {
        var e = el('sp-day');
        var t = e.value ? new Date(e.value + 'T12:00:00') : new Date();
        t.setDate(t.getDate() + d);
        e.value = t.getFullYear() + '-' + String(t.getMonth() + 1).padStart(2, '0') + '-' + String(t.getDate()).padStart(2, '0');
        board();
    }

    function today() {
        var t = new Date();
        el('sp-day').value = t.getFullYear() + '-' + String(t.getMonth() + 1).padStart(2, '0') + '-' + String(t.getDate()).padStart(2, '0');
        board();
    }

    function assign(no) {
        api('Assign', [
            { key: 'no', vlu: no },
            { key: 'courier', vlu: val('sp-c-' + no) },
            { key: 'track', vlu: val('sp-t-' + no) }
        ]);
    }

    function mark(no, what) {
        var q = {
            dispatch: { title: 'Send this order out?', text: 'It is marked Out for delivery. Make sure a courier is chosen.', ok: 'Send out' },
            ready: { title: 'Mark ready for pickup?', text: 'The customer can collect it now.', ok: 'Mark ready' },
            deliver: { title: 'Mark as delivered?', text: 'This closes the order. Orders paid on delivery are marked as paid.', ok: 'Mark delivered' }
        }[what];
        ask(q, function () {
            api('Mark', [
                { key: 'no', vlu: no },
                { key: 'act', vlu: what },
                { key: 'day', vlu: val('sp-day') },
                { key: 'way', vlu: val('sp-way') }
            ]);
        });
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeMenu();
                if (!askBox || !askBox.classList.contains('sp-open')) {
                    closeModal();
                }
            }
        });
        var m = el('sp-modal');
        if (m) {
            m.addEventListener('input', markDirty);
            m.addEventListener('change', markDirty);
            m.addEventListener('mousedown', function (e) {
                if (e.target === m) {
                    closeModal();
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
        board: board,
        shift: shift,
        today: today,
        assign: assign,
        mark: mark
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminShippingJs.reveal);
} else {
    AdminShippingJs.reveal();
}
