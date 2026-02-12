/**
 * chart-interop.js - Interop Chart.js pour Blazor
 */
window.chartInterop = {
    charts: {},

    createChart: function (canvasId, configJson) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return false;

        // Détruire le chart existant si besoin
        if (this.charts[canvasId]) {
            this.charts[canvasId].destroy();
        }

        const config = JSON.parse(configJson);
        this.charts[canvasId] = new Chart(canvas, config);
        return true;
    },

    updateChart: function (canvasId, configJson) {
        if (!this.charts[canvasId]) {
            return this.createChart(canvasId, configJson);
        }

        const config = JSON.parse(configJson);
        const chart = this.charts[canvasId];
        chart.data = config.data;
        if (config.options) chart.options = config.options;
        chart.update();
        return true;
    },

    destroyChart: function (canvasId) {
        if (this.charts[canvasId]) {
            this.charts[canvasId].destroy();
            delete this.charts[canvasId];
        }
    }
};
