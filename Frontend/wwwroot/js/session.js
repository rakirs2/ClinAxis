window.visitorSession = {
  _key: 'clinicaltrialdata_visitor_session',

  get: function () {
    let id = null;
    try {
      id = localStorage.getItem(this._key);
    } catch (e) {
      // localStorage unavailable — fall back to a fresh in-memory id
    }
    if (!id) {
      id = this._generate();
      try {
        localStorage.setItem(this._key, id);
      } catch (e) {
        // Keep the in-memory id for this page load only
      }
    }
    return id;
  },

  _generate: function () {
    if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
      return crypto.randomUUID();
    }
    // Fallback for environments without crypto.randomUUID
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
      const r = (Math.random() * 16) | 0;
      const v = c === 'x' ? r : (r & 0x3) | 0x8;
      return v.toString(16);
    });
  }
};