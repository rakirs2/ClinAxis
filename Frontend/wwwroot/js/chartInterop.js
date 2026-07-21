window.chartInterop = {
  charts: {},

  createChart: function (canvasId, type, labels, datasets, options) {
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;

    if (this.charts[canvasId]) {
      this.charts[canvasId].destroy();
    }

    this.charts[canvasId] = new Chart(ctx, {
      type: type || 'bar',
      data: {
        labels: labels,
        datasets: datasets
      },
      options: options || {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: true, position: 'top' }
        },
        scales: {
          x: { display: true },
          y: {
            display: true,
            title: { display: true, text: 'Duration (ms)' }
          }
        }
      }
    });
  },

  createBarChart: function (canvasId, labels, datasets, options) {
    this.createChart(canvasId, 'bar', labels, datasets, options);
  },

  destroyChart: function (canvasId) {
    if (this.charts[canvasId]) {
      this.charts[canvasId].destroy();
      delete this.charts[canvasId];
    }
  }
};