window.getLocation = () => new Promise((resolve, reject) => {
    if (!navigator.geolocation) {
        reject(new Error('no geolocation'));
        return;
    }
    navigator.geolocation.getCurrentPosition(
        pos => resolve({ lat: pos.coords.latitude, lon: pos.coords.longitude }),
        err => reject(new Error(err.message)),
        { timeout: 10000 });
});

let _historyChart;
window.updateHistoryChart = (canvasId, labels, temp, hum, wind, rain, light) => {
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;
    const data = {
        labels: labels,
        datasets: [
            { label: 'Nhiệt độ (°C)', data: temp, borderColor: '#d9534f', backgroundColor: '#d9534f33', tension: 0.25, pointRadius: 0 },
            { label: 'Độ ẩm (%)', data: hum, borderColor: '#0275d8', backgroundColor: '#0275d833', tension: 0.25, pointRadius: 0 },
            { label: 'Gió (m/s)', data: wind, borderColor: '#5cb85c', backgroundColor: '#5cb85c33', tension: 0.25, pointRadius: 0, yAxisID: 'y2' },
            { label: 'Mưa (mm/h)', data: rain, borderColor: '#292b2c', backgroundColor: '#292b2c33', tension: 0.25, pointRadius: 0, yAxisID: 'y2' },
            { label: 'Ánh sáng (lx)', data: light, borderColor: '#f0ad4e', backgroundColor: '#f0ad4e33', tension: 0.25, pointRadius: 0, yAxisID: 'y3' }
        ]
    };
    if (_historyChart) {
        _historyChart.data = data;
        _historyChart.update('none');
        return;
    }
    _historyChart = new Chart(ctx, {
        type: 'line',
        data: data,
        options: {
            animation: false,
            scales: {
                y: { position: 'left' },
                y2: { position: 'right', grid: { drawOnChartArea: false } },
                y3: { position: 'right', grid: { drawOnChartArea: false } }
            }
        }
    });
};

window.addEventListener('beforeunload', () => { if (_historyChart) _historyChart.destroy(); });
