var ContactJs = (function () {

    var timer = null;
    var ttimer = null;
    var mem = [];

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Contact/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('cn-toast');
        t.textContent = msg;
        t.classList.add('cn-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('cn-open');
        }, 3200);
    }

    function openNav() {
        el('cn-nav').classList.add('cn-open');
        el('cn-scrim').classList.add('cn-open');
    }

    function closeNav() {
        el('cn-nav').classList.remove('cn-open');
        el('cn-scrim').classList.remove('cn-open');
    }

    function search(value) {
        var v = (value || '').trim();
        clearTimeout(timer);
        if (v.length < 2) {
            el('cn-sugg').classList.remove('cn-open');
            return;
        }
        timer = setTimeout(function () {
            call('Suggest', [{ key: 'typed', vlu: v }]);
        }, 250);
    }

    function openSugg() {
        el('cn-sugg').classList.add('cn-open');
    }

    function go() {
        var v = val('cn-q');
        location.href = 'Shop' + (v ? '?term=' + encodeURIComponent(v) : '');
        return false;
    }

    function cartRead() {
        try {
            var raw = localStorage.getItem('pb_cart');
            var list = raw ? JSON.parse(raw) : [];
            mem = Array.isArray(list) ? list : [];
        } catch (e) {
        }
        return mem;
    }

    function cartWrite(list) {
        mem = list;
        try {
            localStorage.setItem('pb_cart', JSON.stringify(list));
        } catch (e) {
        }
        paintCart();
    }

    function paintCart() {
        var n = cartRead().reduce(function (sum, i) {
            return sum + (i.q || 0);
        }, 0);
        var b = el('cn-cartn');
        b.textContent = n;
        b.classList.toggle('cn-has', n > 0);
    }

    function money(v) {
        return '$' + Number(v).toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    }

    function send() {
        call('Send', [
            { key: 'name', vlu: val('cn-name') },
            { key: 'email', vlu: val('cn-email') },
            { key: 'topic', vlu: val('cn-topic') },
            { key: 'message', vlu: val('cn-msg') }
        ]);
        return false;
    }

    function sent() {
        el('cn-name').value = '';
        el('cn-email').value = '';
        el('cn-msg').value = '';
        el('cn-topic').value = '';
        toast('Message sent');
    }

    function reveal() {
        paintCart();
        document.addEventListener('click', function (e) {
            var s = el('cn-sugg');
            if (s && !s.parentElement.contains(e.target)) {
                s.classList.remove('cn-open');
            }
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
            }
        });
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        search: search,
        openSugg: openSugg,
        go: go,
        send: send,
        sent: sent
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', ContactJs.reveal);
} else {
    ContactJs.reveal();
}
