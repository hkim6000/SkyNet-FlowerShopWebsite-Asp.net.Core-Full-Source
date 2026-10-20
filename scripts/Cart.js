var CartJs = (function () {

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
        $ApiRequest('Cart/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('ct-toast');
        t.textContent = msg;
        t.classList.add('ct-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ct-open');
        }, 3200);
    }

    function openNav() {
        el('ct-nav').classList.add('ct-open');
        el('ct-scrim').classList.add('ct-open');
    }

    function closeNav() {
        el('ct-nav').classList.remove('ct-open');
        el('ct-scrim').classList.remove('ct-open');
    }

    function search(value) {
        var v = (value || '').trim();
        clearTimeout(timer);
        if (v.length < 2) {
            el('ct-sugg').classList.remove('ct-open');
            return;
        }
        timer = setTimeout(function () {
            call('Suggest', [{ key: 'typed', vlu: v }]);
        }, 250);
    }

    function openSugg() {
        el('ct-sugg').classList.add('ct-open');
    }

    function go() {
        var v = val('ct-q');
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
        var b = el('ct-cartn');
        b.textContent = n;
        b.classList.toggle('ct-has', n > 0);
    }

    function money(v) {
        return '$' + Number(v).toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    }

    var timer2 = null;

    function payVal() {
        var r = document.querySelector('input[name="ct-pay"]:checked');
        return r ? r.value : '';
    }

    function cartJson() {
        return JSON.stringify(cartRead());
    }

    function reprice() {
        if (cartRead().length === 0) {
            el('ct-empty').hidden = false;
            el('ct-layout').hidden = true;
            return;
        }
        call('Price', [
            { key: 'cart', vlu: cartJson() },
            { key: 'way', vlu: mode() },
            { key: 'zipcode', vlu: val('ct-zip') }
        ]);
    }

    function priced(ok, count) {
        el('ct-empty').hidden = count > 0;
        el('ct-layout').hidden = count === 0;
        el('ct-place').disabled = !ok;
    }

    function mode() {
        var pick = document.querySelector('input[name="ct-mode"]:checked');
        var way = pick ? pick.value : 'delivery';
        el('ct-addr').hidden = way === 'pickup';
        el('ct-datelab').textContent = way === 'pickup' ? 'Pickup date' : 'Delivery date';
        return way;
    }

    function modeChanged() {
        mode();
        reprice();
    }

    function zipTyped() {
        clearTimeout(timer2);
        timer2 = setTimeout(reprice, 350);
    }

    function step(i, d) {
        var list = cartRead().slice();
        if (!list[i]) {
            return;
        }
        list[i].q = Math.max(1, Math.min(20, list[i].q + d));
        cartWrite(list);
        reprice();
    }

    function drop(i) {
        var list = cartRead().slice();
        list.splice(i, 1);
        cartWrite(list);
        reprice();
    }

    function same() {
        var box = el('ct-same');
        var on = !!(box && box.checked);
        var rn = el('ct-rname');
        var rp = el('ct-rphone');
        if (on) {
            rn.value = val('ct-name');
            rp.value = val('ct-phone');
            el('ct-e-rname').textContent = '';
            el('ct-e-rphone').textContent = '';
        } else {
            if (rn.value.trim() === val('ct-name')) {
                rn.value = '';
            }
            if (rp.value.trim() === val('ct-phone')) {
                rp.value = '';
            }
        }
        rn.readOnly = on;
        rp.readOnly = on;
    }

    function follow() {
        var box = el('ct-same');
        if (box && box.checked) {
            el('ct-rname').value = val('ct-name');
            el('ct-rphone').value = val('ct-phone');
        }
    }

    function place() {
        follow();
        var way = mode();
        call('Place', [
            { key: 'cart', vlu: cartJson() },
            { key: 'way', vlu: way },
            { key: 'zipcode', vlu: val('ct-zip') },
            { key: 'name', vlu: val('ct-name') },
            { key: 'email', vlu: val('ct-email') },
            { key: 'phone', vlu: val('ct-phone') },
            { key: 'rname', vlu: val('ct-rname') },
            { key: 'rphone', vlu: val('ct-rphone') },
            { key: 'street', vlu: val('ct-street') },
            { key: 'city', vlu: val('ct-city') },
            { key: 'day', vlu: val('ct-date') },
            { key: 'slot', vlu: val('ct-slot') },
            { key: 'card', vlu: val('ct-msg') },
            { key: 'instr', vlu: val('ct-note') },
            { key: 'pay', vlu: payVal() }
        ]);
        return false;
    }

    function placed(url) {
        cartWrite([]);
        location.href = url;
    }

    function reveal() {
        paintCart();
        document.addEventListener('click', function (e) {
            var s = el('ct-sugg');
            if (s && !s.parentElement.contains(e.target)) {
                s.classList.remove('ct-open');
            }
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
            }
        });
        el('ct-name').addEventListener('input', follow);
        el('ct-phone').addEventListener('input', follow);
        if (el('ct-same').checked) {
            same();
        }
        mode();
        reprice();
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        search: search,
        openSugg: openSugg,
        go: go,
        mode: mode,
        modeChanged: modeChanged,
        zipTyped: zipTyped,
        step: step,
        drop: drop,
        same: same,
        place: place,
        placed: placed,
        priced: priced,
        reprice: reprice
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', CartJs.reveal);
} else {
    CartJs.reveal();
}
