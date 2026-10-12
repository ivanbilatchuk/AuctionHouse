/* AuctionHouse_Community (Проєкт №3: MongoDB). Запуск: mongosh --file collections.js. Ідемпотентний (створення або collMod + createIndex).
   EMBEDDING: comments/replies, sales, recentReviews, expert. REFERENCE: discussionId, itemId/auctionId/userId (інші контексти, без FK).
   ГІБРИД: expertOpinions, sellerRatings. ГНУЧКА СХЕМА: expertOpinions.attributes залежить від categoryKind. UUID користувачів = ExternalUserId із SQL. */

const dbName = 'AuctionHouse_Community';
const database = db.getSiblingDB(dbName);

const idLong   = { bsonType: ['int', 'long'] };
const uuid     = { bsonType: 'binData', description: 'UUID користувача з Identity-контексту' };
const money    = { bsonType: 'decimal', minimum: 0 };

// Створює колекцію з валідатором або оновлює валідатор існуючої (ідемпотентно).
function ensureCollection(name, jsonSchema) {
    const options = {
        validator: { $jsonSchema: jsonSchema },
        validationLevel: 'strict',
        validationAction: 'error'
    };
    if (database.getCollectionNames().includes(name)) {
        database.runCommand(Object.assign({ collMod: name }, options));
        print(`collMod   ${name}`);
    } else {
        database.createCollection(name, options);
        print(`created   ${name}`);
    }
}

// 1. expertOpinions -- висновки експертів про автентичність.
// Приклад ГНУЧКОЇ схеми: у карт атрибути gradingCompany/grade, у марок
// perforation/watermark, у вінілу matrixNumber/pressing, у монет
// metal/weightGrams. Відповідність categoryKind <-> набір обов'язкових
// атрибутів перевіряє oneOf на рівні валідатора.
const attributesBranch = (kind, required, extraProps) => ({
    properties: {
        categoryKind: { enum: [kind] },
        attributes: {
            bsonType: 'object',
            required: required,
            properties: extraProps
        }
    }
});

ensureCollection('expertOpinions', {
    bsonType: 'object',
    required: ['itemId', 'itemTitle', 'categoryKind', 'expert', 'verdict', 'confidence', 'attributes', 'createdAt'],
    properties: {
        itemId:       Object.assign({ description: 'Catalog.Item.ItemId (посилання без FK)' }, idLong),
        itemTitle:    { bsonType: 'string', minLength: 1, description: 'ДЕНОРМАЛІЗОВАНИЙ знімок назви з Catalog' },
        auctionId:    idLong,
        discussionId: { bsonType: 'objectId', description: 'REFERENCE на discussions._id' },
        categoryKind: { enum: ['cards', 'stamps', 'vinyl', 'coins'] },
        expert: {                                    // EMBEDDED знімок експерта
            bsonType: 'object',
            required: ['userId', 'displayName'],
            properties: {
                userId:      uuid,
                displayName: { bsonType: 'string', minLength: 1 },
                credentials: { bsonType: 'array', items: { bsonType: 'string' } }
            }
        },
        verdict:      { enum: ['authentic', 'questionable', 'counterfeit'] },
        confidence:   { bsonType: 'int', minimum: 0, maximum: 100 },
        attributes:   { bsonType: 'object', description: 'Структура залежить від categoryKind (див. oneOf)' },
        notes:        { bsonType: 'string', maxLength: 4000 },
        createdAt:    { bsonType: 'date' },
        updatedAt:    { bsonType: 'date' },
        schemaVersion:{ bsonType: 'int', minimum: 1 }
    },
    oneOf: [
        attributesBranch('cards',  ['gradingCompany', 'grade'], {
            gradingCompany: { bsonType: 'string' },
            grade:          { bsonType: ['int', 'double'], minimum: 1, maximum: 10 }
        }),
        attributesBranch('stamps', ['perforation', 'watermark'], {
            perforation: { bsonType: 'string' },
            watermark:   { bsonType: 'string' }
        }),
        attributesBranch('vinyl',  ['matrixNumber', 'pressing'], {
            matrixNumber: { bsonType: 'string' },
            pressing:     { bsonType: 'string' }
        }),
        attributesBranch('coins',  ['metal', 'weightGrams'], {
            metal:       { bsonType: 'string' },
            weightGrams: { bsonType: ['int', 'double', 'decimal'], minimum: 0 }
        })
    ]
});

// Запит: «висновки по предмету, найновіші першими» (картка лота).
database.expertOpinions.createIndex({ itemId: 1, createdAt: -1 }, { name: 'ix_itemId_createdAt' });
// UNIQUE: один експерт -- один висновок на предмет.
database.expertOpinions.createIndex({ itemId: 1, 'expert.userId': 1 }, { name: 'ux_itemId_expert', unique: true });
// Запит: «усі сумнівні/підроблені висновки категорії» (модерація).
database.expertOpinions.createIndex({ categoryKind: 1, verdict: 1 }, { name: 'ix_category_verdict' });
// Запит: «висновки, прив'язані до обговорення» (sparse: поле необов'язкове).
database.expertOpinions.createIndex({ discussionId: 1 }, { name: 'ix_discussionId', sparse: true });

// 2. discussions -- одне обговорення на аукціон; коментарі та відповіді
// ВБУДОВАНІ (вкладені масиви й об'єкти) -- читаються одним запитом.
// Ризик необмеженого росту документа компенсується лімітом коментарів
// на рівні застосунку (bucket pattern -- у наступних лабораторних).
const authorSchema = {
    bsonType: 'object',
    required: ['userId', 'displayName'],
    properties: { userId: uuid, displayName: { bsonType: 'string', minLength: 1 } }
};
const replySchema = {
    bsonType: 'object',
    required: ['commentId', 'author', 'text', 'createdAt'],
    properties: {
        commentId: { bsonType: 'objectId' },
        author:    authorSchema,
        text:      { bsonType: 'string', minLength: 1, maxLength: 2000 },
        createdAt: { bsonType: 'date' }
    }
};

ensureCollection('discussions', {
    bsonType: 'object',
    required: ['auctionId', 'itemId', 'itemTitle', 'status', 'comments', 'commentCount', 'createdAt'],
    properties: {
        auctionId:    Object.assign({ description: 'Auctions.Auction.AuctionId (без FK)' }, idLong),
        itemId:       idLong,
        itemTitle:    { bsonType: 'string', minLength: 1, description: 'ДЕНОРМАЛІЗОВАНИЙ знімок назви' },
        status:       { enum: ['open', 'locked'] },
        commentCount: { bsonType: 'int', minimum: 0, description: 'ДЕНОРМАЛІЗОВАНИЙ лічильник (коментарі + відповіді)' },
        comments: {
            bsonType: 'array',
            items: {
                bsonType: 'object',
                required: ['commentId', 'author', 'text', 'createdAt'],
                properties: {
                    commentId: { bsonType: 'objectId' },
                    author:    authorSchema,
                    text:      { bsonType: 'string', minLength: 1, maxLength: 2000 },
                    createdAt: { bsonType: 'date' },
                    replies:   { bsonType: 'array', items: replySchema }
                }
            }
        },
        createdAt: { bsonType: 'date' },
        updatedAt: { bsonType: 'date' }
    }
});

// UNIQUE: одне обговорення на аукціон (та сама ідея, що UNIQUE у SQL).
database.discussions.createIndex({ auctionId: 1 }, { name: 'ux_auctionId', unique: true });
// Запит: «обговорення всіх аукціонів предмета».
database.discussions.createIndex({ itemId: 1 }, { name: 'ix_itemId' });
// Multikey: «де коментував користувач» (профіль учасника).
database.discussions.createIndex({ 'comments.author.userId': 1 }, { name: 'ix_comment_author' });

// 3. priceHistory -- історія продажів предмета + ДЕНОРМАЛІЗОВАНА статистика
// (stats) для швидких читань без агрегації. Оновлюється подією AuctionSettled.
ensureCollection('priceHistory', {
    bsonType: 'object',
    required: ['itemId', 'itemTitle', 'categoryKind', 'sales', 'stats'],
    properties: {
        itemId:       idLong,
        itemTitle:    { bsonType: 'string', minLength: 1 },
        categoryKind: { enum: ['cards', 'stamps', 'vinyl', 'coins'] },
        sales: {
            bsonType: 'array',
            items: {
                bsonType: 'object',
                required: ['auctionId', 'finalPrice', 'currency', 'soldAt'],
                properties: {
                    auctionId:  idLong,
                    finalPrice: money,
                    currency:   { bsonType: 'string', minLength: 3, maxLength: 3 },
                    soldAt:     { bsonType: 'date' },
                    source:     { bsonType: 'string' }
                }
            }
        },
        stats: {
            bsonType: 'object',
            required: ['saleCount'],
            properties: {
                saleCount: { bsonType: 'int', minimum: 0 },
                avgPrice:  money,
                minPrice:  money,
                maxPrice:  money,
                lastPrice: money
            }
        },
        updatedAt: { bsonType: 'date' }
    }
});

database.priceHistory.createIndex({ itemId: 1 }, { name: 'ux_itemId', unique: true });
// Запит: «найдорожчі категорії за середньою ціною» (аналітика).
database.priceHistory.createIndex({ categoryKind: 1, 'stats.avgPrice': -1 }, { name: 'ix_category_avgPrice' });

// 4. sellerRatings -- ГІБРИД: агрегована статистика + вбудовані останні відгуки.
// Повна історія відгуків (необмежена) лишається референсом за sellerId.
ensureCollection('sellerRatings', {
    bsonType: 'object',
    required: ['sellerId', 'sellerName', 'stats', 'recentReviews'],
    properties: {
        sellerId:   uuid,
        sellerName: { bsonType: 'string', minLength: 1, description: 'ДЕНОРМАЛІЗОВАНЕ імʼя з Identity' },
        stats: {
            bsonType: 'object',
            required: ['average', 'count'],
            properties: {
                average:      { bsonType: ['double', 'int'], minimum: 0, maximum: 5 },
                count:        { bsonType: 'int', minimum: 0 },
                distribution: { bsonType: 'object' }
            }
        },
        recentReviews: {
            bsonType: 'array',
            maxItems: 20,
            items: {
                bsonType: 'object',
                required: ['reviewId', 'buyerId', 'buyerName', 'auctionId', 'score', 'createdAt'],
                properties: {
                    reviewId:  { bsonType: 'objectId' },
                    buyerId:   uuid,
                    buyerName: { bsonType: 'string' },
                    auctionId: idLong,
                    itemTitle: { bsonType: 'string' },
                    score:     { bsonType: 'int', minimum: 1, maximum: 5 },
                    comment:   { bsonType: 'string', maxLength: 2000 },
                    createdAt: { bsonType: 'date' }
                }
            }
        },
        updatedAt: { bsonType: 'date' }
    }
});

database.sellerRatings.createIndex({ sellerId: 1 }, { name: 'ux_sellerId', unique: true });
// Запит: «топ продавців за рейтингом».
database.sellerRatings.createIndex({ 'stats.average': -1, 'stats.count': -1 }, { name: 'ix_rating_top' });

print('AuctionHouse_Community: collections and indexes are ready.');
