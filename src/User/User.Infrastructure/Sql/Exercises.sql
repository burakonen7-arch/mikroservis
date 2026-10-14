-- Run these queries individually in a PostgreSQL console. This file is not executed by the API.

-- 1. Verify the initial seed count.
SELECT COUNT(*) AS user_count FROM users;

-- 2. Group users by city and calculate average age.
SELECT city, COUNT(*) AS user_count, ROUND(AVG(age), 1) AS average_age
FROM users
GROUP BY city
ORDER BY user_count DESC, city;

-- 3. HAVING filters groups after aggregation.
SELECT city, COUNT(*) AS user_count
FROM users
WHERE age >= 30
GROUP BY city
HAVING COUNT(*) >= 5
ORDER BY user_count DESC, city;

-- 4. A CTE and a window function find the three oldest users in each city.
WITH ranked_users AS
(
    SELECT id, first_name, last_name, city, age,
           ROW_NUMBER() OVER (PARTITION BY city ORDER BY age DESC, id) AS position
    FROM users
)
SELECT *
FROM ranked_users
WHERE position <= 3
ORDER BY city, position;

-- 5. Registration counts by month.
SELECT date_trunc('month', created_at) AS month, COUNT(*) AS registrations
FROM users
GROUP BY date_trunc('month', created_at)
ORDER BY month;

-- 6. Inspect the actual query plan. With only 100 rows a sequential scan can be optimal.
EXPLAIN (ANALYZE, BUFFERS)
SELECT id, first_name, last_name, age
FROM users
WHERE age >= 30
ORDER BY age DESC, id
LIMIT 10;
