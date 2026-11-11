cd /data || exit 1
for d in *; do
  case "$d" in media|lost+found) ;; *) rm -rf "/data/$d" ;; esac
done
rm -rf /cache/*
ls /data; df /data /cache
cat /sys/class/power_supply/battery/capacity
