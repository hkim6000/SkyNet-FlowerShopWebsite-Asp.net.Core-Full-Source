var ProductJs = (function () {

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
        $ApiRequest('Product/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('pd-toast');
        t.textContent = msg;
        t.classList.add('pd-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('pd-open');
        }, 3200);
    }

    function openNav() {
        el('pd-nav').classList.add('pd-open');
        el('pd-scrim').classList.add('pd-open');
    }

    function closeNav() {
        el('pd-nav').classList.remove('pd-open');
        el('pd-scrim').classList.remove('pd-open');
    }

    function search(value) {
        var v = (value || '').trim();
        clearTimeout(timer);
        if (v.length < 2) {
            el('pd-sugg').classList.remove('pd-open');
            return;
        }
        timer = setTimeout(function () {
            call('Suggest', [{ key: 'typed', vlu: v }]);
        }, 250);
    }

    function openSugg() {
        el('pd-sugg').classList.add('pd-open');
    }

    function go() {
        var v = val('pd-q');
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
        var b = el('pd-cartn');
        b.textContent = n;
        b.classList.toggle('pd-has', n > 0);
    }

    function money(v) {
        return '$' + Number(v).toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    }

    function sizeVal() {
        var r = document.querySelector('input[name="pd-size"]:checked');
        return r ? r.value : '';
    }

    function extras() {
        var list = [];
        document.querySelectorAll('input[name="pd-extra"]:checked').forEach(function (c) {
            list.push(c.value);
        });
        return list;
    }

    function qtyVal() {
        var n = parseInt(el('pd-qty').value, 10);
        n = isNaN(n) ? 1 : Math.max(1, Math.min(20, n));
        el('pd-qty').value = n;
        return n;
    }

    function step(d) {
        el('pd-qty').value = qtyVal() + d;
        quote();
    }

    function quote() {
        call('Quote', [
            { key: 'item', vlu: val('pd-item') },
            { key: 'sz', vlu: sizeVal() },
            { key: 'ex', vlu: extras().join(',') },
            { key: 'qty', vlu: String(qtyVal()) }
        ]);
    }

    function add() {
        call('Add', [
            { key: 'item', vlu: val('pd-item') },
            { key: 'sz', vlu: sizeVal() },
            { key: 'ex', vlu: extras().join(',') },
            { key: 'qty', vlu: String(qtyVal()) }
        ]);
    }

    function added(item) {
        var list = cartRead().slice();
        var sig = item.a.slice().sort().join(',');
        var hit = list.filter(function (i) {
            return i.k === item.k && i.s === item.s && (i.a || []).slice().sort().join(',') === sig;
        })[0];
        if (hit) {
            hit.q = Math.min(20, hit.q + item.q);
        } else {
            list.push(item);
        }
        cartWrite(list);
        toast('Added to your cart');
        var b = el('pd-add');
        b.textContent = 'Added. View cart';
        b.onclick = function () {
            location.href = 'Cart';
        };
    }

    function reset() {
        var b = el('pd-add');
        b.textContent = 'Add to cart';
        b.onclick = add;
    }

    function stock(ok) {
        el('pd-add').disabled = !ok;
    }

    function photo(btn) {
        var main = el('pd-main');
        if (!main || !btn) {
            return;
        }
        var vid = el('pd-video');
        if (btn.getAttribute('data-kind') === 'video') {
            if (vid.getAttribute('src') !== btn.getAttribute('data-src')) {
                vid.src = btn.getAttribute('data-src');
            }
            vid.hidden = false;
            main.hidden = true;
            var p = vid.play();
            if (p && p.catch) {
                p.catch(function () { });
            }
        } else {
            vid.pause();
            vid.hidden = true;
            main.hidden = false;
            main.src = btn.getAttribute('data-src');
        }
        document.querySelectorAll('#pd-thumbs .pd-th').forEach(function (b) {
            var on = b === btn;
            b.classList.toggle('pd-th-on', on);
            b.setAttribute('aria-pressed', on ? 'true' : 'false');
        });
    }

    function reveal() {
        paintCart();
        document.addEventListener('click', function (e) {
            var s = el('pd-sugg');
            if (s && !s.parentElement.contains(e.target)) {
                s.classList.remove('pd-open');
            }
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
            }
        });
        document.querySelectorAll('input[name="pd-size"], input[name="pd-extra"]').forEach(function (c) {
            c.addEventListener('change', function () {
                reset();
                quote();
            });
        });
        if (el('pd-item') && el('pd-item').value) {
            quote();
        }
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        search: search,
        openSugg: openSugg,
        go: go,
        sizeVal: sizeVal,
        step: step,
        quote: quote,
        add: add,
        added: added,
        stock: stock,
        photo: photo
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', ProductJs.reveal);
} else {
    ProductJs.reveal();
}
