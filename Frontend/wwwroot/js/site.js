async function doSearch() {
    const q = document.getElementById('searchInput').value;
    const res = await fetch('/api-proxy/studies?search=' + encodeURIComponent(q) + '&page=1&pageSize=50');
    const data = await res.json();
    const tbody = document.getElementById('resultsBody');
    tbody.innerHTML = '';
    if (data.data && data.data.length > 0) {
        for (const s of data.data) {
            tbody.innerHTML += '<tr><td><a href="/studies/' + s.nctId + '">' + s.nctId + '</a></td><td>' + s.briefTitle + '</td><td>' + s.overallStatus + '</td></tr>';
        }
        document.getElementById('results').style.display = '';
        document.getElementById('noResults').style.display = 'none';
    } else {
        document.getElementById('results').style.display = 'none';
        document.getElementById('noResults').style.display = '';
    }
}
