-- 範例主題的資料表與 view，與正式專案的 deploy/database/init/*.sql 同一個位置、同一個寫法。
-- 寫好之後套到資料庫，把表與 view 列進 BuildModels 的清單，執行 BuildModels 產生 Models/Site/。
-- 範例用自己的本機庫，不碰正式庫：
--   docker run -d --name vaeron-sample-db -e POSTGRES_USER=site -e POSTGRES_PASSWORD=site-sample -e POSTGRES_DB=site -p 127.0.0.1:5499:5432 postgres:16
--   docker exec -i vaeron-sample-db psql -U site -d site < deploy/database/init/10_site.sql

CREATE SCHEMA IF NOT EXISTS dbo;

-- 項目與分類選單的選項是同一張表，以 type 區分；每多一種就多一個 type 值，不多開表。
CREATE TABLE dbo.item_management (
    id serial PRIMARY KEY,
    type character varying(10) NOT NULL,
    owner_id integer,
    code character varying(20) NOT NULL,
    name character varying(100) NOT NULL,
    group_name character varying(50),
    kind character varying(20),
    due_at timestamp with time zone,
    file_paths text[] NOT NULL DEFAULT '{}',
    removed_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    updated_at timestamp with time zone NOT NULL DEFAULT now()
);

-- 同一個人的項目代碼不重複；分類選項的代碼全站不重複（owner_id 是空的）。
CREATE UNIQUE INDEX item_management_code ON dbo.item_management (type, coalesce(owner_id, 0), code);

COMMENT ON TABLE dbo.item_management IS '項目與分類選單的選項。';
COMMENT ON COLUMN dbo.item_management.type IS 'item＝一個項目；kind＝分類選單上的一個選項。';
COMMENT ON COLUMN dbo.item_management.owner_id IS '[item] 持有者的帳號編號；[kind] 空的，每個人都用同一份選單。';
COMMENT ON COLUMN dbo.item_management.code IS '代碼。';
COMMENT ON COLUMN dbo.item_management.name IS '名稱。';
COMMENT ON COLUMN dbo.item_management.group_name IS '[kind] 屬於哪一個大類，選單照它分組。';
COMMENT ON COLUMN dbo.item_management.kind IS '[item] 選的分類代碼，指向 kind 列的 code；沒選是空的。';
COMMENT ON COLUMN dbo.item_management.due_at IS '[item] 期限。';
COMMENT ON COLUMN dbo.item_management.file_paths IS '[item] 附件在 Storage/item/ 底下的相對位置，每次上傳接在後面。';
COMMENT ON COLUMN dbo.item_management.removed_at IS '[item] 移掉的時刻；刪除是狀態，列不真的刪。';

-- 這一頁讀的每一樣東西都從這一支來：item 列是一個項目，kind 列是分類選單上的一個選項。
-- 每一列帶著看得到它的帳號（account_id），查詢直接比對登入的人；kind 列是空的，每個人都看得到。
CREATE VIEW dbo.vw_item AS
SELECT 'item'::character varying(10) AS type,
       i.owner_id AS account_id,
       i.id,
       i.code,
       i.name,
       NULL::character varying(50) AS group_name,
       i.kind,
       coalesce(k.name, '—')::character varying(100) AS kind_name,
       i.due_at,
       to_char(i.due_at AT TIME ZONE 'Asia/Taipei', 'YYYY-MM-DD HH24:MI')::character varying(16) AS due_text,
       round((extract(epoch FROM i.due_at - now()) / 3600)::numeric, 1) AS remaining_hours,
       row_number() OVER (PARTITION BY i.owner_id ORDER BY i.due_at, i.id)::integer AS rank,
       i.file_paths
  FROM dbo.item_management AS i
  LEFT JOIN dbo.item_management AS k ON k.type = 'kind' AND k.code = i.kind
 WHERE i.type = 'item'
   AND i.removed_at IS NULL
UNION ALL
SELECT 'kind',
       NULL,
       k.id,
       k.code,
       k.name,
       k.group_name,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       NULL,
       '{}'
  FROM dbo.item_management AS k
 WHERE k.type = 'kind'
 ORDER BY type, rank, group_name, name;

COMMENT ON VIEW dbo.vw_item IS '項目列表與分類選單：item 是一個還沒移掉的項目，最急的排在最前面；kind 是分類選單上的一個選項。';
COMMENT ON COLUMN dbo.vw_item.type IS 'item＝一個項目；kind＝分類選單上的一個選項。';
COMMENT ON COLUMN dbo.vw_item.account_id IS '[item] 看得到這一列的帳號編號，查詢直接比對登入的人；[kind] 空的，每個人都看得到。';
COMMENT ON COLUMN dbo.vw_item.group_name IS '[kind] 屬於哪一個大類，選單照它畫成 optgroup。';
COMMENT ON COLUMN dbo.vw_item.kind IS '[item] 選的分類代碼；沒選是空的。';
COMMENT ON COLUMN dbo.vw_item.kind_name IS '[item] 選的分類寫在畫面上的字；沒選分類是破折號。';
COMMENT ON COLUMN dbo.vw_item.due_text IS '[item] 期限照 Asia/Taipei 寫成 YYYY-MM-DD HH24:MI。';
COMMENT ON COLUMN dbo.vw_item.remaining_hours IS '[item] 離期限還有幾小時，取到小數一位；過了期限是負值，照樣列出。';
COMMENT ON COLUMN dbo.vw_item.rank IS '[item] 在他的項目裡照期限由近到遠排第幾，從 1 起算。';
COMMENT ON COLUMN dbo.vw_item.file_paths IS '[item] 附件在 Storage/item/ 底下的相對位置。';

-- 分類選單的起始選項。
INSERT INTO dbo.item_management (type, code, name, group_name) VALUES
    ('kind', 'repair', '維修', '服務'),
    ('kind', 'visit', '拜訪', '服務'),
    ('kind', 'quote', '報價', '業務'),
    ('kind', 'contract', '合約', '業務');
