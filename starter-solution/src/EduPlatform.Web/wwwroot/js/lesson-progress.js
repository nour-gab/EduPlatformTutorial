// wwwroot/js/lesson-progress.js — Ch.18 §18.4
// Polls every 30 seconds to record lesson watch time.
(function () {
    var minutesWatched = 0;
    var token = document.querySelector('input[name="__RequestVerificationToken"]');
    if (!token) return;

    setInterval(function () {
        minutesWatched += 0.5;
        fetch('/MyCourses/RecordProgress', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': token.value
            },
            body: new URLSearchParams({
                courseSlug:   window.courseSlug   || '',
                lessonOrder:  window.lessonOrder  || 0,
                minutes:      minutesWatched,
                __RequestVerificationToken: token.value
            })
        });
    }, 30000);
})();
