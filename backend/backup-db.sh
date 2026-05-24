#!/bin/bash
# Backup fashionshop.db trước khi làm bất cứ thao tác phá hủy nào
# Chạy: ./backup-db.sh
cd "$(dirname "$0")"
if [ ! -f fashionshop.db ]; then
    echo "Không có fashionshop.db để backup"
    exit 0
fi
mkdir -p .backups
TS=$(date +%Y%m%d_%H%M%S)
cp fashionshop.db ".backups/fashionshop_${TS}.db"
echo "✓ Backed up to .backups/fashionshop_${TS}.db"
# Giữ 10 backup gần nhất, xóa cũ hơn
ls -t .backups/fashionshop_*.db 2>/dev/null | tail -n +11 | xargs rm -f 2>/dev/null
