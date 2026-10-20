var AdminOrdersJs = (function () {

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
        $ApiRequest('AdminOrders/' + method, JSON.stringify(list));
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
        var t = el('ao-toast');
        t.textContent = msg;
        t.classList.add('ao-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ao-open');
        }, 3400);
    }

    function openMenu() {
        el('ao-rail').classList.add('ao-open');
        el('ao-scrim').classList.add('ao-open');
    }

    function closeMenu() {
        el('ao-rail').classList.remove('ao-open');
        el('ao-scrim').classList.remove('ao-open');
    }

    function openModal(title) {
        var m = el('ao-modal');
        el('ao-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('ao-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('ao-modal');
        if (m) {
            m.classList.remove('ao-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('ao-modal');
        if (!m || !m.classList.contains('ao-open')) {
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
        if (!askBox || !askBox.classList.contains('ao-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('ao-open');
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
        askBox.className = 'ao-ask';
        askBox.innerHTML = '<div class="ao-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="ao-err" role="alert"></em><div class="ao-askacts"><button type="button" class="ao-btn ao-btn-ghost"></button><button type="button" class="ao-btn"></button></div></div>';
        document.getElementById('ao-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('ao-open')) {
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
        x.b[1].className = 'ao-btn' + (o.danger ? ' ao-askdel' : '');
        askBox.firstChild.classList.toggle('ao-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('ao-open');
        if (input) {
            x.i.focus();
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    function esc(s) {
        return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    var timer = null;
    var current = '';

    function filters(page) {
        return [
            { key: 'term', vlu: val('ao-term') },
            { key: 'st', vlu: val('ao-st') },
            { key: 'py', vlu: val('ao-py') },
            { key: 'pg', vlu: String(page || 1) }
        ];
    }

    var pageNo = 1;

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

    function open(no) {
        current = no;
        api('Open', [{ key: 'no', vlu: no }]);
    }

    function opened(no) {
        openModal('Order ' + no);
    }

    function act(no, what) {
        var q = {
            prepare: { title: 'Start arranging this order?', text: 'It moves to Being arranged.', ok: 'Start arranging' },
            paid: { title: 'Mark this order as paid?', text: 'Use this when you have collected the payment.', ok: 'Mark as paid' },
            cancel: { title: 'Cancel this order?', text: 'The bouquets go back into stock. If it was paid by card you can refund it next.', value: '', place: 'Reason (optional)', ok: 'Cancel order', no: 'Keep it', danger: true },
            refund: { title: 'Refund the payment?', text: 'The full amount goes back to the customer. This cannot be undone.', ok: 'Refund', no: 'Keep payment', danger: true }
        }[what];
        ask(q, function (why) {
            send(no, what, why || '');
        });
    }

    function send(no, what, why) {
        var list = filters(pageNo);
        list.push({ key: 'no', vlu: no }, { key: 'act', vlu: what }, { key: 'why', vlu: why });
        api('Act', list);
    }

    function addNote(no) {
        var text = val('ao-note');
        if (text.length < 2) {
            toast('Please write a note first.');
            return;
        }
        send(no, 'note', text);
    }

    function done(msg) {
        toast(msg);
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeMenu();
                if (!askBox || !askBox.classList.contains('ao-open')) {
                    closeModal();
                }
            }
        });
        var m = el('ao-modal');
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
        list: list,
        type: type,
        open: open,
        opened: opened,
        act: act,
        addNote: addNote,
        done: done
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminOrdersJs.reveal);
} else {
    AdminOrdersJs.reveal();
}
