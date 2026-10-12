/* AuctionHouse_Community: приклади запитів та aggregate pipeline. Запуск: mongosh --file queries.js */
const database = db.getSiblingDB('AuctionHouse_Community');

print('--- 1. Висновки по предмету, найновіші першими (індекс ix_itemId_createdAt)');
printjson(database.expertOpinions
    .find({ itemId: NumberLong('1') }, { itemTitle: 1, verdict: 1, confidence: 1, 'expert.displayName': 1 })
    .sort({ createdAt: -1 }).toArray());

print('--- 2. Гнучка схема: картки з оцінкою PSA >= 9 (запит по полю, яке є лише в частини документів)');
printjson(database.expertOpinions
    .find({ categoryKind: 'cards', 'attributes.gradingCompany': 'PSA', 'attributes.grade': { $gte: 9 } },
          { itemTitle: 1, 'attributes.grade': 1 }).toArray());

print('--- 3. Вкладений масив: усі коментарі користувача (multikey-індекс ix_comment_author)');
printjson(database.discussions.aggregate([
    { $match: { 'comments.author.userId': UUID('33333333-3333-3333-3333-333333333333') } },
    { $unwind: '$comments' },
    { $match: { 'comments.author.userId': UUID('33333333-3333-3333-3333-333333333333') } },
    { $project: { _id: 0, itemTitle: 1, text: '$comments.text', createdAt: '$comments.createdAt',
                  repliesCount: { $size: '$comments.replies' } } }
]).toArray());

print('--- 4. $lookup між колекціями (reference expertOpinions.discussionId -> discussions._id)');
printjson(database.expertOpinions.aggregate([
    { $match: { discussionId: { $exists: true } } },
    { $lookup: { from: 'discussions', localField: 'discussionId', foreignField: '_id', as: 'discussion' } },
    { $unwind: '$discussion' },
    { $project: { _id: 0, itemTitle: 1, verdict: 1, commentsInDiscussion: '$discussion.commentCount' } }
]).toArray());

print('--- 5. Агрегація: середня впевненість експертів і кількість висновків за категоріями');
printjson(database.expertOpinions.aggregate([
    { $group: { _id: '$categoryKind', opinions: { $sum: 1 }, avgConfidence: { $avg: '$confidence' } } },
    { $sort: { avgConfidence: -1 } }
]).toArray());

print('--- 6. Перерахунок статистики цін з вбудованого масиву sales ($unwind + $group)');
printjson(database.priceHistory.aggregate([
    { $unwind: '$sales' },
    { $group: { _id: '$itemId', itemTitle: { $first: '$itemTitle' },
                saleCount: { $sum: 1 }, avgPrice: { $avg: '$sales.finalPrice' }, maxPrice: { $max: '$sales.finalPrice' } } }
]).toArray());

print('--- 7. Топ продавців за рейтингом (індекс ix_rating_top)');
printjson(database.sellerRatings
    .find({}, { sellerName: 1, 'stats.average': 1, 'stats.count': 1 })
    .sort({ 'stats.average': -1, 'stats.count': -1 }).limit(5).toArray());

print('--- 8. Валідація схеми: документ без обов\'язкового поля має бути відхилений');
try {
    database.expertOpinions.insertOne({ itemId: NumberLong('1'), itemTitle: 'invalid sample' });
    print('UNEXPECTED: invalid document was inserted');
} catch (e) {
    print('OK, rejected by JSON Schema validation (code ' + e.code + '): ' + String(e.message).split('\n')[0]);
}
