(function () {
    var panel = document.getElementById('meta-panel');
    if (!panel) return;

    document.querySelectorAll('[data-meta]').forEach(function (el) {
        el.addEventListener('click', function (e) {
            e.stopPropagation();
            document.querySelectorAll('[data-meta].active').forEach(function (a) {
                a.classList.remove('active');
            });
            el.classList.add('active');
            try {
                renderMetaPanel(panel, JSON.parse(el.getAttribute('data-meta')));
            } catch (_) {
                panel.innerHTML = '<div class="meta-placeholder">Blad odczytu metadanych.</div>';
            }
        });
    });

    function renderMetaPanel(panel, m) {
        var html = '';

        if (m.isAmendmentContent) {
            html += '<div class="meta-amendment-banner">&#9998; Tre&#347;&#263; nowelizacji</div>';
        }

        html += section('Identyfikator', '<span class="meta-eid">' + esc(m.eid || '') + '</span>');
        html += section('Typ', badge(m.unitType, m.unitTypeLabel));

        if (m.number) {
            var numDetails = esc(m.number);
            if (m.numericPart) numDetails += ' <span style="color:var(--muted)">(nr: ' + m.numericPart + ')</span>';
            if (m.lexicalPart) numDetails += ' <span style="color:var(--muted)">lit: ' + esc(m.lexicalPart) + '</span>';
            if (m.superscript) numDetails += '<sup>' + esc(m.superscript) + '</sup>';
            html += kv('Numer', numDetails);
        }

        if (m.isImplicit !== undefined) html += kv('Dorozumiany', m.isImplicit ? 'Tak' : 'Nie');
        if (m.role) html += kv('Rola', esc(m.role));
        if (m.isAmending) html += kv('Art. zmieniajacy', 'Tak');
        if (m.paragraphsCount !== undefined) html += kv('Usepy', m.paragraphsCount);
        if (m.textSegmentsCount) html += kv('Segmenty tekstu', m.textSegmentsCount);
        if (m.commonPartsCount) html += kv('Czesci wspolne', m.commonPartsCount);
        if (m.hasAmendment) html += kv('Nowelizacja', 'Tak');
        if (m.introText) html += kv('Wpr. do wyliczenia', '<span class="meta-intro-text">' + esc(m.introText) + '</span>');
        if (m.effectiveDate && m.effectiveDate !== '0001-01-01') html += kv('Data wejscia w zycie', esc(m.effectiveDate));

        if (m.contentText) {
            html += '<div class="meta-section"><h3>Tresc (podglad)</h3>';
            html += '<div class="meta-content-preview">' + esc(m.contentText) + '</div></div>';
        }

        if (m.journals && m.journals.length) {
            html += section('Dzienniki ustaw', m.journals.map(function (j) { return esc(j); }).join('<br>'));
        }

        if (m.amendment) {
            var am = m.amendment;
            var amClass = am.operationType === 'Repeal' ? 'meta-amendment-repeal' : 'meta-amendment-positive';
            var amHtml = '<span class="' + amClass + '">' + esc(am.operationTypeLabel) + '</span>';
            if (am.targetAct) amHtml += kv('Akt docelowy', esc(am.targetAct));
            if (am.targets && am.targets.length) amHtml += kv('Cele', esc(am.targets.join('; ')));
            if (am.effectiveDate) amHtml += kv('Wejscie w zycie', esc(am.effectiveDate));
            html += '<div class="meta-section"><h3>Nowelizacja</h3>' + amHtml + '</div>';
        }

        if (m.validationMessages && m.validationMessages.length) {
            var vmHtml = m.validationMessages.map(function (vm) {
                return '<div class="meta-validation-' + vm.level.toLowerCase() + '">'
                    + levelIcon(vm.level) + ' ' + esc(vm.message) + '</div>';
            }).join('');
            html += '<div class="meta-section"><h3>Walidacja</h3>' + vmHtml + '</div>';
        }

        panel.innerHTML = html;
    }

    function section(title, content) {
        return '<div class="meta-section"><h3>' + esc(title) + '</h3>' + content + '</div>';
    }
    function kv(k, v) {
        return '<div class="meta-kv"><span class="k">' + esc(k) + ':</span><span class="v">' + v + '</span></div>';
    }
    function badge(type, label) {
        var t = (type || '').toLowerCase();
        return '<span class="meta-badge badge-' + t + '">' + esc(label || type) + '</span>';
    }
    function levelIcon(level) {
        return level === 'Info' ? '\u2139' : level === 'Warning' ? '\u26a0' : level === 'Error' ? '\u2716' : '\u203c';
    }
    function esc(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }
})();
