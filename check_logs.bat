@echo off
echo Android cihaz log'larini kontrol ediyor...
echo.
echo GooglePlayReview log'lari:
adb logcat -s Unity GooglePlayReview
echo.
echo Tum log'lari gormek icin:
echo adb logcat
pause

