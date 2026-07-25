window.meshTree = {
  render(containerId, nodes, selected) {
    const container = document.getElementById(containerId);
    if (!container) return;
    container.innerHTML = this._buildTree(nodes, selected || []);
    container.addEventListener('change', (e) => {
      const cb = e.target;
      if (cb.type !== 'checkbox') return;
      const tn = cb.value;
      if (!tn) return;
      this._setChildren(container, tn, cb.checked);
    });
    container.addEventListener('click', (e) => {
      const toggle = e.target.closest('.mt-toggle');
      if (!toggle) return;
      const node = toggle.closest('.mt-node');
      if (!node) return;
      const children = node.querySelector('.mt-children');
      if (!children) return;
      const expanded = children.style.display !== 'none';
      children.style.display = expanded ? 'none' : '';
      toggle.textContent = expanded ? '▶' : '▼';
    });
  },

  getSelected(containerId) {
    const container = document.getElementById(containerId);
    if (!container) return [];
    const cbs = container.querySelectorAll('input[type="checkbox"]:checked');
    return Array.from(cbs).map(cb => cb.value).filter(v => v);
  },

  _buildTree(nodes, selected) {
    let html = '';
    for (const node of nodes) {
      const checked = selected.includes(node.treeNumber) ? 'checked' : '';
      html += `<div class="mt-node">`;
      html += `<div class="mt-row">`;
      if (node.hasChildren) {
        html += `<span class="mt-toggle">▶</span>`;
      } else {
        html += `<span class="mt-toggle" style="visibility:hidden;">▶</span>`;
      }
      html += `<input type="checkbox" class="form-check-input me-1" value="${this._esc(node.treeNumber)}" id="mt_${this._esc(node.treeNumber)}" ${checked}>`;
      html += `<label class="form-check-label" for="mt_${this._esc(node.treeNumber)}" style="font-size:0.9em;">`;
      html += `${this._esc(node.treeNumber)} — ${this._esc(node.name)} <span class="text-muted">(${node.studyCount})</span>`;
      html += `</label></div>`;
      if (node.children && node.children.length > 0) {
        html += `<div class="mt-children" style="margin-left:24px;display:none;">`;
        html += this._buildTree(node.children, selected);
        html += `</div>`;
      }
      html += `</div>`;
    }
    return html;
  },

  _setChildren(container, treeNumber, checked) {
    const prefix = treeNumber + '.';
    const cbs = container.querySelectorAll('input[type="checkbox"]');
    for (const cb of cbs) {
      if (cb.value === treeNumber) continue;
      if (cb.value.startsWith(prefix)) {
        cb.checked = checked;
      }
    }
  },

  _esc(s) {
    if (!s) return '';
    return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }
};
