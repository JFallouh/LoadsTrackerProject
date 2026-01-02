WITH
bol_rnk AS (
    SELECT
        t.DETAIL_NUMBER AS DETAIL_LINE_ID,
        t.TRACE_NUMBER  AS BOL_NO,
        ROW_NUMBER() OVER (
            PARTITION BY t.DETAIL_NUMBER
            ORDER BY      t.TRACE_ID DESC
        ) AS rn
    FROM TMWIN.TRACE t
    WHERE t.DESC IN ('BOL', 'BOL #')
),
bol_one AS (
    SELECT DETAIL_LINE_ID, BOL_NO
    FROM bol_rnk
    WHERE rn = 1
),

order_rnk AS (
    SELECT
        t.DETAIL_NUMBER AS DETAIL_LINE_ID,
        t.TRACE_NUMBER  AS ORDER_NO,
        ROW_NUMBER() OVER (
            PARTITION BY t.DETAIL_NUMBER
            ORDER BY      t.TRACE_ID DESC
        ) AS rn
    FROM TMWIN.TRACE t
    WHERE t.DESC = 'ORDER #'
),
order_one AS (
    SELECT DETAIL_LINE_ID, ORDER_NO
    FROM order_rnk
    WHERE rn = 1
),

tl_filtered AS (
    SELECT tl.*
    FROM   TMWIN.TLORDER tl
    WHERE  tl.PICK_UP_BY >= (CURRENT TIMESTAMP - 1 MONTH)
       AND COALESCE(tl.DELIVER_BY_END, tl.DELIVER_BY) <= (CURRENT TIMESTAMP + 1 MONTH)

       -- Optional (same idea as your old query):
       -- AND tl.CURRENT_STATUS NOT IN (
       --   'APPRVD','ARRCONS','AVAIL','BILLD','BR-ARRCONS','CANCL',
       --   'COMPLETE','ENTRY','LOCKED','PRINTED','QUOTE',
       --   'REFUSED','REPRINTED','UNAPPRVD'
       -- )
)

SELECT
    tl.DETAIL_LINE_ID,
    tl.BILL_NUMBER,
    COALESCE(b.BOL_NO,   '') AS "BOL #",
    COALESCE(o.ORDER_NO, '') AS "ORDER #",

    tl.DESTINATION,
    tl.DESTNAME,
    tl.DESTCITY,
    tl.DESTPROV,

    tl.CUSTOMER,
    tl.CALLNAME,

    tl.ORIGIN,
    tl.ORIGNAME,
    tl.ORIGCITY,
    tl.ORIGPROV,

    tl.PICK_UP_BY,
    tl.PICK_UP_BY_END,
    tl.DELIVER_BY,
    tl.DELIVER_BY_END,

    tl.CURRENT_STATUS,
    tl.PALLETS,
    tl.CUBE,
    tl.WEIGHT,
    tl.DANGEROUS_GOODS,
    tl.REQUESTED_EQUIPMEN
FROM tl_filtered tl
LEFT JOIN bol_one   b ON b.DETAIL_LINE_ID = tl.DETAIL_LINE_ID
LEFT JOIN order_one o ON o.DETAIL_LINE_ID = tl.DETAIL_LINE_ID
ORDER BY tl.PICK_UP_BY, tl.DETAIL_LINE_ID;


/*If you meant a single window like “pickup OR delivery is within [-1 month, +1 month]”, replace the WHERE with:
WHERE COALESCE(tl.PICK_UP_BY, tl.PICK_UP_BY_END) BETWEEN (CURRENT TIMESTAMP - 1 MONTH) AND (CURRENT TIMESTAMP + 1 MONTH)
   OR COALESCE(tl.DELIVER_BY, tl.DELIVER_BY_END) BETWEEN (CURRENT TIMESTAMP - 1 MONTH) AND (CURRENT TIMESTAMP + 1 MONTH)
*/