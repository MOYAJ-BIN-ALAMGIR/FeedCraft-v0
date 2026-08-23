// Dynamic rows for the formulation input form.
//
// Everything here is *panel-relative*, because the same input form is rendered twice on the
// experiment page. A "panel" is the root element emitted by Views/Shared/_FormulationInputs.cshtml:
//
//     <div class="formulation-panel" data-prefix="PanelA.Formulation.">
//
// and data-prefix is the ASP.NET field-name prefix that partial was rendered with — empty on
// /Feed, "PanelA.Formulation." / "PanelB.Formulation." on /Experiment. The prefix comes from the
// same ViewData.TemplateInfo.HtmlFieldPrefix the tag helpers use to emit the names, so the names
// the browser posts and the names this file builds cannot drift apart.
//
// Two rules keep the two panels from bleeding into each other:
//   1. Never query `document` for a form element — always query inside the panel.
//   2. Never write a field name as a literal — always build it through field()/the prefix.
//
// Only `name` attributes are managed here, never `id`: model binding reads names, and the
// generated rows have no ids to collide.

function hidden(name, value) {
    return '<input type="hidden" name="' + name + '" value="' + value + '" />';
}

// Resolves the panel an event came from. Accepts a control inside the panel, or the panel
// itself (closest() matches the element it is called on).
function panelOf(el) {
    return el.closest('.formulation-panel');
}

function panelPrefix(panel) {
    return panel.getAttribute('data-prefix') || '';
}

// A full posted field name for this panel, e.g. "PanelA.Formulation.Ingredients[0].Name".
function field(panel, name) {
    return panelPrefix(panel) + name;
}

// The prefix contains '.' characters, which are regex metacharacters — an unescaped prefix
// would still match, just not only where it should.
function escapeForRegExp(text) {
    return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function ingredientBody(panel) {
    return panel.querySelector('.ingredients-table tbody');
}

function nutrientBody(panel) {
    return panel.querySelector('.nutrients-table tbody');
}

function nutrientHeaders(panel) {
    return panel.querySelectorAll('.ingredients-header-row th.nutrient-col');
}

// Ids must be unique across the posted form; new rows take max+1. Scoped to the panel, so a
// new row in panel A never depends on what panel B contains.
function nextId(panel, collection) {
    var selector = 'input[name^="' + field(panel, collection) + '["][name$=".Id"]';
    var max = 0;
    panel.querySelectorAll(selector).forEach(function (el) {
        var n = parseInt(el.value, 10);
        if (!isNaN(n) && n > max) max = n;
    });
    return max + 1;
}

function addIngredientRow(el) {
    var panel = panelOf(el);
    var body = ingredientBody(panel);
    var index = body.rows.length;
    var row = body.insertRow(index);
    var newId = nextId(panel, 'Ingredients');
    var item = field(panel, 'Ingredients[' + index + ']');

    var html =
        '<td>' + hidden(item + '.Id', newId) +
            '<input name="' + item + '.Name" class="form-control" value="New Ingredient" /></td>' +
        '<td><input name="' + item + '.CostPerUnit" class="form-control" type="number" step="0.01" value="0" /></td>';

    // A value cell per existing nutrient, in the same order as the headers.
    var nutrientRows = nutrientBody(panel).rows;
    for (var k = 0; k < nutrientRows.length; k++) {
        var nutrientId = nutrientRows[k].querySelector('input[name^="' + field(panel, 'NutrientDefinitions[') + '"][name$=".Id"]').value;
        html += '<td class="nutrient-cell">' +
            hidden(item + '.NutrientValues[' + k + '].NutrientDefinitionId', nutrientId) +
            '<input name="' + item + '.NutrientValues[' + k + '].Value" class="form-control" type="number" step="0.01" value="0" /></td>';
    }

    html +=
        '<td class="incl-min-cell"><input name="' + item + '.MinInclusionPct" class="form-control" type="number" step="0.1" placeholder="0" /></td>' +
        '<td class="incl-max-cell"><input name="' + item + '.MaxInclusionPct" class="form-control" type="number" step="0.1" placeholder="100" value="100" /></td>' +
        '<td><button type="button" class="btn btn-danger btn-sm" onclick="removeRow(this)">X</button></td>';

    row.innerHTML = html;
}

// Adds a nutrient: one row in the constraints table, and one column
// across every ingredient row.
function addNutrient(el) {
    var panel = panelOf(el);
    var body = nutrientBody(panel);
    var k = body.rows.length;
    var newId = nextId(panel, 'NutrientDefinitions');
    var row = body.insertRow(k);
    var def = field(panel, 'NutrientDefinitions[' + k + ']');
    var con = field(panel, 'Constraints[' + k + ']');

    row.innerHTML =
        '<td>' + hidden(def + '.Id', newId) +
            '<input name="' + def + '.Name" class="form-control" value="New Nutrient" oninput="syncNutrientHeaders(this)" /></td>' +
        '<td><input name="' + def + '.Unit" class="form-control" value="%" oninput="syncNutrientHeaders(this)" /></td>' +
        '<td class="text-center align-middle">' +
            '<input type="checkbox" name="' + def + '.IsPercentage" value="true" checked class="form-check-input" />' +
            hidden(def + '.IsPercentage', 'false') + '</td>' +
        '<td>' + hidden(con + '.NutrientDefinitionId', newId) +
            '<input name="' + con + '.MinValue" class="form-control" type="number" step="0.01" /></td>' +
        '<td><input name="' + con + '.MaxValue" class="form-control" type="number" step="0.01" /></td>' +
        '<td><button type="button" class="btn btn-danger btn-sm" onclick="removeNutrient(this)">X</button></td>';

    // New header column, inserted before the inclusion-limit headers.
    var th = document.createElement('th');
    th.className = 'nutrient-col';
    var thMin = panel.querySelector('.th-min-pct');
    thMin.parentNode.insertBefore(th, thMin);

    // New value cell on every ingredient row.
    var rows = ingredientBody(panel).rows;
    for (var i = 0; i < rows.length; i++) {
        var cellIndex = rows[i].querySelectorAll('td.nutrient-cell').length;
        var item = field(panel, 'Ingredients[' + i + ']');
        var td = document.createElement('td');
        td.className = 'nutrient-cell';
        td.innerHTML =
            hidden(item + '.NutrientValues[' + cellIndex + '].NutrientDefinitionId', newId) +
            '<input name="' + item + '.NutrientValues[' + cellIndex + '].Value" class="form-control" type="number" step="0.01" value="0" />';
        rows[i].insertBefore(td, rows[i].querySelector('td.incl-min-cell'));
    }

    syncNutrientHeaders(panel);
}

function removeNutrient(btn) {
    var panel = panelOf(btn);
    var row = btn.closest('tr');
    var k = row.rowIndex - 1; // rowIndex counts the header row
    row.parentNode.removeChild(row);

    // Drop the matching header and the matching cell on every ingredient row.
    var headers = nutrientHeaders(panel);
    if (headers[k]) headers[k].parentNode.removeChild(headers[k]);

    var rows = ingredientBody(panel).rows;
    for (var i = 0; i < rows.length; i++) {
        var cells = rows[i].querySelectorAll('td.nutrient-cell');
        if (cells[k]) rows[i].removeChild(cells[k]);
    }

    reIndexNutrients(panel);
    reIndexRows(panel);
    syncNutrientHeaders(panel);
}

function removeRow(btn) {
    var panel = panelOf(btn);
    var row = btn.closest('tr');
    row.parentNode.removeChild(row);
    // Re-index so model binding still sees a contiguous list after a delete.
    reIndexRows(panel);
}

// Rewrites Ingredients[i] on every input, including the nested
// NutrientValues[k] indices, which are keyed off cell position.
function reIndexRows(panel) {
    var head = field(panel, 'Ingredients');
    var pattern = new RegExp('^' + escapeForRegExp(head) + '\\[\\d+\\]');
    var rows = ingredientBody(panel).rows;

    for (var i = 0; i < rows.length; i++) {
        (function (index) {
            rows[index].querySelectorAll('input').forEach(function (input) {
                if (input.name.indexOf(head + '[') === 0) {
                    input.name = input.name.replace(pattern, head + '[' + index + ']');
                }
            });

            rows[index].querySelectorAll('td.nutrient-cell').forEach(function (cell, k) {
                cell.querySelectorAll('input').forEach(function (input) {
                    // Unanchored, and the inner index is prefix-independent.
                    input.name = input.name.replace(/NutrientValues\[\d+\]/, 'NutrientValues[' + k + ']');
                });
            });
        })(i);
    }
}

function reIndexNutrients(panel) {
    var defHead = field(panel, 'NutrientDefinitions');
    var conHead = field(panel, 'Constraints');
    var defPattern = new RegExp('^' + escapeForRegExp(defHead) + '\\[\\d+\\]');
    var conPattern = new RegExp('^' + escapeForRegExp(conHead) + '\\[\\d+\\]');
    var rows = nutrientBody(panel).rows;

    for (var k = 0; k < rows.length; k++) {
        (function (index) {
            rows[index].querySelectorAll('input').forEach(function (input) {
                input.name = input.name
                    .replace(defPattern, defHead + '[' + index + ']')
                    .replace(conPattern, conHead + '[' + index + ']');
            });
        })(k);
    }
}

// Keeps the ingredient-table column headers in step with the nutrient names/units.
function syncNutrientHeaders(el) {
    var panel = panelOf(el);
    var headers = nutrientHeaders(panel);
    var rows = nutrientBody(panel).rows;
    for (var k = 0; k < headers.length && k < rows.length; k++) {
        var name = rows[k].querySelector('input[name$=".Name"]');
        var unit = rows[k].querySelector('input[name$=".Unit"]');
        headers[k].textContent = (name ? name.value : '') + (unit && unit.value ? ' (' + unit.value + ')' : '');
    }
}
