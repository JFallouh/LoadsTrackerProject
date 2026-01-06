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


tldtl_rnk AS (
    SELECT
        d.ORDER_ID,
        d.CUBE_UNITS,
        d.WEIGHT_UNITS,
        d.TEMPERATURE,
        d.TEMPERATURE_UNITS,
        ROW_NUMBER() OVER (
            PARTITION BY d.ORDER_ID
            ORDER BY
                CASE WHEN d.TEMPERATURE IS NOT NULL THEN 0 ELSE 1 END,
                d.SEQUENCE
        ) AS rn
    FROM TMWIN.TLDTL d
),
tldtl_one AS (
    SELECT
        ORDER_ID,
        CUBE_UNITS,
        WEIGHT_UNITS,
        TEMPERATURE,
        TEMPERATURE_UNITS
    FROM tldtl_rnk
    WHERE rn = 1
),

tl_filtered AS (
    SELECT tl.*
    FROM   TMWIN.TLORDER tl
    WHERE  tl.PICK_UP_BY >= (CURRENT TIMESTAMP - 1 MONTH)
       AND COALESCE(tl.DELIVER_BY_END, tl.DELIVER_BY) <= (CURRENT TIMESTAMP + 1 MONTH)
       AND tl.CUSTOMER IN ('18396','45289')
),
reason_src AS (
    SELECT DISTINCT
        o.ORDER_ID AS DETAIL_LINE_ID,
        s.SF_SHORT_DESC
    FROM tl_filtered tl
    JOIN TMWIN.ODRSTAT o
           ON o.ORDER_ID = tl.DETAIL_LINE_ID
    JOIN TMWIN.SERVICE_FAILURE_CODES s
           ON s.SF_REASON_CODE = o.SF_REASON_CODE
    WHERE o.SF_REASON_CODE IS NOT NULL
    AND s.SF_SHORT_DESC IS NOT NULL
),
reason_agg AS (
    SELECT
        DETAIL_LINE_ID,
        LISTAGG(SF_SHORT_DESC, ', ') WITHIN GROUP (ORDER BY SF_SHORT_DESC) AS SF_SHORT_DESC
    FROM reason_src
    GROUP BY DETAIL_LINE_ID
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
    tl.ACTUAL_DELIVERY,
    COALESCE(r.SF_SHORT_DESC, '') AS "SF_SHORT_DESC",

    tl.CURRENT_STATUS,
    tl.PALLETS,
    tl.CUBE,
    dtl.CUBE_UNITS,
    tl.WEIGHT,
    dtl.WEIGHT_UNITS,

    
    
    dtl.TEMPERATURE,
    dtl.TEMPERATURE_UNITS,

    tl.DANGEROUS_GOODS,
    tl.REQUESTED_EQUIPMEN
FROM tl_filtered tl
LEFT JOIN bol_one   b   ON b.DETAIL_LINE_ID = tl.DETAIL_LINE_ID
LEFT JOIN order_one o   ON o.DETAIL_LINE_ID = tl.DETAIL_LINE_ID
LEFT JOIN tldtl_one dtl ON dtl.ORDER_ID     = tl.DETAIL_LINE_ID
LEFT JOIN reason_agg r   ON r.DETAIL_LINE_ID = tl.DETAIL_LINE_ID
ORDER BY tl.PICK_UP_BY, tl.DETAIL_LINE_ID;
