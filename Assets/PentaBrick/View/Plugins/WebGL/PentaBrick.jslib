// WebGL 빌드에서 브라우저 기능을 쓰기 위한 함수들. C# 쪽은 View/Web.cs.
mergeInto(LibraryManager.library, {
  // 클립보드에 복사. clipboard API가 막히면(오래된 브라우저, http 주소) 숨긴 입력 칸으로 복사한다.
  PentaBrick_CopyText: function (ptr) {
    var text = UTF8ToString(ptr);
    function fallback() {
      var area = document.createElement('textarea');
      area.value = text;
      area.style.position = 'fixed';
      area.style.opacity = '0';
      document.body.appendChild(area);
      area.select();
      try { document.execCommand('copy'); } catch (e) {}
      document.body.removeChild(area);
    }
    if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(text).catch(fallback);
    else fallback();
  },

  // 주소의 ?뒤를 바꾼다. replaceState라 뒤로 가기 기록은 남지 않는다(웹판 src/main.js와 같음).
  PentaBrick_SetQuery: function (ptr) {
    history.replaceState(null, '', location.pathname + UTF8ToString(ptr));
  }
});
