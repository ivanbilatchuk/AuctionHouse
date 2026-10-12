/* AuctionHouse_Community: початкові дані. Ідемпотентний: updateOne({_id}, {$setOnInsert}, {upsert: true}) із фіксованими _id.
   GUID та ItemId/AuctionId збігаються з seed-ами Auctions і Catalog. */

const database = db.getSiblingDB('AuctionHouse_Community');

// Вставляє документ, лише якщо документа з таким _id ще немає.
function insertOnce(collectionName, doc) {
    const { _id, ...rest } = doc;
    const result = database.getCollection(collectionName)
        .updateOne({ _id: _id }, { $setOnInsert: rest }, { upsert: true });
    return result.upsertedCount;
}

const SELLER  = UUID('11111111-1111-1111-1111-111111111111');
const OLENA   = UUID('22222222-2222-2222-2222-222222222222');
const MARKO   = UUID('33333333-3333-3333-3333-333333333333');
const IRYNA   = UUID('44444444-4444-4444-4444-444444444444');
const EXPERT  = UUID('55555555-5555-5555-5555-555555555555');

const expertSnapshot = {
    userId: EXPERT,
    displayName: 'Dr. Andriy Savchuk',
    credentials: ['Certified appraiser', '15 years in collectibles authentication']
};

const oid = (n) => ObjectId('65a000000000000000000' + String(n).padStart(3, '0'));

let inserted = 0;

// discussions (EMBEDDED коментарі + відповіді)
inserted += insertOnce('discussions', {
    _id: oid(101),
    auctionId: NumberLong('1'),
    itemId: NumberLong('1'),
    itemTitle: 'Charizard Holo 1st Edition, Base Set 1999 (PSA 9)',
    status: 'open',
    commentCount: NumberInt(4),
    comments: [
        {
            commentId: oid(201),
            author: { userId: MARKO, displayName: 'Marko Bondar' },
            text: 'Is the PSA slab free of scratches? Could you add a photo of the back?',
            createdAt: ISODate('2026-10-10T09:15:00Z'),
            replies: [
                {
                    commentId: oid(202),
                    author: { userId: SELLER, displayName: 'Vintage Vault' },
                    text: 'Yes, the case is clean. Back photo added to the lot gallery.',
                    createdAt: ISODate('2026-10-10T10:02:00Z')
                },
                {
                    commentId: oid(203),
                    author: { userId: MARKO, displayName: 'Marko Bondar' },
                    text: 'Thank you, that settles it.',
                    createdAt: ISODate('2026-10-10T10:20:00Z')
                }
            ]
        },
        {
            commentId: oid(204),
            author: { userId: IRYNA, displayName: 'Iryna Melnyk' },
            text: 'Does the seller ship insured outside the EU?',
            createdAt: ISODate('2026-10-11T12:40:00Z'),
            replies: []
        }
    ],
    createdAt: ISODate('2026-10-09T08:00:00Z')
});

inserted += insertOnce('discussions', {
    _id: oid(102),
    auctionId: NumberLong('2'),
    itemId: NumberLong('2'),
    itemTitle: 'Penny Black, 1840, Great Britain',
    status: 'open',
    commentCount: NumberInt(1),
    comments: [
        {
            commentId: oid(205),
            author: { userId: OLENA, displayName: 'Olena Kovalenko' },
            text: 'Are the four margins clear on all sides?',
            createdAt: ISODate('2026-10-11T18:05:00Z'),
            replies: []
        }
    ],
    createdAt: ISODate('2026-10-11T08:00:00Z')
});

// expertOpinions (ГНУЧКА схема + REFERENCE на discussions)
inserted += insertOnce('expertOpinions', {
    _id: oid(301),
    itemId: NumberLong('1'),
    itemTitle: 'Charizard Holo 1st Edition, Base Set 1999 (PSA 9)',   // денормалізований знімок
    auctionId: NumberLong('1'),
    discussionId: oid(101),                                          // reference на discussions._id
    categoryKind: 'cards',
    expert: expertSnapshot,                                          // embedded знімок
    verdict: 'authentic',
    confidence: NumberInt(96),
    attributes: {
        gradingCompany: 'PSA',
        grade: 9,
        certificateNumber: 'PSA-60000001',
        printRun: '1st Edition, shadowless',
        holoPattern: 'Original foil, no peeling'
    },
    notes: 'Print texture, font and holo pattern are consistent with the authentic 1999 first print run.',
    createdAt: ISODate('2026-10-09T12:00:00Z'),
    schemaVersion: NumberInt(1)
});

inserted += insertOnce('expertOpinions', {
    _id: oid(302),
    itemId: NumberLong('2'),
    itemTitle: 'Penny Black, 1840, Great Britain',
    auctionId: NumberLong('2'),
    discussionId: oid(102),
    categoryKind: 'stamps',
    expert: expertSnapshot,
    verdict: 'authentic',
    confidence: NumberInt(91),
    attributes: {
        perforation: 'Imperforate',
        watermark: 'Small Crown',
        plateNumber: '1a',
        margins: { top: 'clear', bottom: 'clear', left: 'clear', right: 'close' },
        cancellation: 'Red Maltese Cross'
    },
    notes: 'Colour and paper type match the 1840 printing; one tight margin on the right.',
    createdAt: ISODate('2026-10-11T09:30:00Z'),
    schemaVersion: NumberInt(1)
});

inserted += insertOnce('expertOpinions', {
    _id: oid(303),
    itemId: NumberLong('3'),
    itemTitle: 'Pink Floyd - The Dark Side of the Moon, UK 1st Press, 1973',
    categoryKind: 'vinyl',
    expert: expertSnapshot,
    verdict: 'questionable',
    confidence: NumberInt(62),
    attributes: {
        matrixNumber: 'SHVL 804 A-2U',
        pressing: 'UK 1st',
        label: 'Harvest',
        extras: ['poster', 'stickers'],
        vinylCondition: 'VG+'
    },
    notes: 'Matrix inscription needs an in-hand check; sleeve print quality is slightly off.',
    createdAt: ISODate('2026-10-12T07:45:00Z'),
    schemaVersion: NumberInt(1)
});

// priceHistory (EMBEDDED sales + ДЕНОРМАЛІЗОВАНА stats)
inserted += insertOnce('priceHistory', {
    _id: oid(401),
    itemId: NumberLong('1'),
    itemTitle: 'Charizard Holo 1st Edition, Base Set 1999 (PSA 9)',
    categoryKind: 'cards',
    sales: [
        { auctionId: NumberLong('9101'), finalPrice: NumberDecimal('9400.00'), currency: 'USD', soldAt: ISODate('2024-03-14T19:00:00Z'), source: 'archive' },
        { auctionId: NumberLong('9102'), finalPrice: NumberDecimal('10650.00'), currency: 'USD', soldAt: ISODate('2025-06-02T19:00:00Z'), source: 'archive' }
    ],
    stats: {
        saleCount: NumberInt(2),
        avgPrice:  NumberDecimal('10025.00'),
        minPrice:  NumberDecimal('9400.00'),
        maxPrice:  NumberDecimal('10650.00'),
        lastPrice: NumberDecimal('10650.00')
    },
    updatedAt: ISODate('2025-06-02T19:05:00Z')
});

inserted += insertOnce('priceHistory', {
    _id: oid(402),
    itemId: NumberLong('2'),
    itemTitle: 'Penny Black, 1840, Great Britain',
    categoryKind: 'stamps',
    sales: [
        { auctionId: NumberLong('9201'), finalPrice: NumberDecimal('480.00'), currency: 'USD', soldAt: ISODate('2025-01-20T18:00:00Z'), source: 'archive' }
    ],
    stats: {
        saleCount: NumberInt(1),
        avgPrice:  NumberDecimal('480.00'),
        minPrice:  NumberDecimal('480.00'),
        maxPrice:  NumberDecimal('480.00'),
        lastPrice: NumberDecimal('480.00')
    },
    updatedAt: ISODate('2025-01-20T18:05:00Z')
});

inserted += insertOnce('priceHistory', {
    _id: oid(403),
    itemId: NumberLong('3'),
    itemTitle: 'Pink Floyd - The Dark Side of the Moon, UK 1st Press, 1973',
    categoryKind: 'vinyl',
    sales: [],
    stats: { saleCount: NumberInt(0) },
    updatedAt: ISODate('2026-10-12T00:00:00Z')
});

// sellerRatings (ГІБРИД: stats + вбудовані відгуки)
inserted += insertOnce('sellerRatings', {
    _id: oid(501),
    sellerId: SELLER,
    sellerName: 'Vintage Vault',
    stats: {
        average: 4.67,
        count: NumberInt(3),
        distribution: { '1': 0, '2': 0, '3': 0, '4': 1, '5': 2 }
    },
    recentReviews: [
        {
            reviewId: oid(601),
            buyerId: OLENA, buyerName: 'Olena Kovalenko',
            auctionId: NumberLong('9101'), itemTitle: 'Charizard Holo 1st Edition (earlier sale)',
            score: NumberInt(5), comment: 'Exactly as described, fast insured shipping.',
            createdAt: ISODate('2024-03-20T10:00:00Z')
        },
        {
            reviewId: oid(602),
            buyerId: MARKO, buyerName: 'Marko Bondar',
            auctionId: NumberLong('9201'), itemTitle: 'Penny Black (earlier sale)',
            score: NumberInt(5), comment: 'Great packaging and communication.',
            createdAt: ISODate('2025-01-25T14:30:00Z')
        },
        {
            reviewId: oid(603),
            buyerId: IRYNA, buyerName: 'Iryna Melnyk',
            auctionId: NumberLong('9102'), itemTitle: 'Charizard Holo 1st Edition (earlier sale)',
            score: NumberInt(4),
            createdAt: ISODate('2025-06-08T09:10:00Z')
        }
    ],
    updatedAt: ISODate('2025-06-08T09:15:00Z')
});

print(`Community seed completed. New documents inserted: ${inserted}`);
printjson({
    expertOpinions: database.expertOpinions.countDocuments(),
    discussions:    database.discussions.countDocuments(),
    priceHistory:   database.priceHistory.countDocuments(),
    sellerRatings:  database.sellerRatings.countDocuments()
});
