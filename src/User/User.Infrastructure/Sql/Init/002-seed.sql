-- 100 deterministic users, 10 cities, varying ages and registration dates.
-- ON CONFLICT lets this seed be manually rerun without duplicating dummy users.
INSERT INTO users (first_name, last_name, email, age, city, created_at)
SELECT
    (ARRAY['Ali', 'Ayse', 'Mehmet', 'Zeynep', 'Can', 'Elif', 'Deniz', 'Ece', 'Burak', 'Selin'])[((n - 1) % 10) + 1],
    (ARRAY['Yilmaz', 'Kaya', 'Demir', 'Celik', 'Sahin', 'Yildiz', 'Aydin', 'Arslan', 'Dogan', 'Kilic'])[((n - 1) / 10) + 1],
    'user' || lpad(n::text, 3, '0') || '@example.com',
    18 + ((n * 7) % 48),
    (ARRAY['Istanbul', 'Ankara', 'Izmir', 'Bursa', 'Antalya', 'Adana', 'Konya', 'Eskisehir', 'Samsun', 'Trabzon'])[((n * 3 - 1) % 10) + 1],
    CURRENT_TIMESTAMP - (n || ' days')::interval
FROM generate_series(1, 100) AS series(n)
ORDER BY n
ON CONFLICT (email) DO NOTHING;
