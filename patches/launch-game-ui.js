// Merge this handler into the existing wwwroot/index.html navigation/button setup.
function requestLaunchGame() {
  if (!window.chrome?.webview) {
    toast('กรุณาเปิดผ่าน HSHONAD Launcher บน Windows');
    return;
  }
  if (!window.__savedGamePath) {
    toast('กรุณาเลือกไฟล์เกม .exe ก่อน');
    window.chrome.webview.postMessage({ type: 'selectGame' });
    return;
  }
  toast('กำลังเปิดเกม...');
  window.chrome.webview.postMessage({ type: 'launchGame' });
}

// Add a Launch Game button with id="launchGameBtn" to the home/start page,
// then wire it once after the DOM is ready:
document.getElementById('launchGameBtn')?.addEventListener('click', requestLaunchGame);

// Listen for the native launch result:
window.chrome?.webview?.addEventListener('message', event => {
  const data = event.data || {};
  if (data.type === 'gameLaunchResult') {
    toast(data.message || (data.ok ? 'เริ่มเกมแล้ว' : 'เปิดเกมไม่สำเร็จ'));
  }
});
