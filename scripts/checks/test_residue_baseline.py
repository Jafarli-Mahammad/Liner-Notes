import json
import os
from pathlib import Path
import tempfile
import unittest
from residue_baseline import make_review, write_private

class ResidueBaselineTests(unittest.TestCase):
    def snapshot(self):
        def pick(id, owner, rank, title):
            return dict(Id=id, ProfileId=owner, Rank=rank, Title=title, ArtistName='Fixture artist',
                        Mbid=title, NormalizedArtistName='fixture artist', NormalizedTitle=title,
                        ScoreBreakdown={'secret': 'do not reveal'}, Feedback='Liked')
        return dict(profiles=[{'Id':'a'}, {'Id':'b'}], picks=[pick('1','a',1,'one'),pick('2','b',2,'foreign'),pick('3','a',3,'one')])

    def test_review_is_owned_concealed_unrated_and_deduplicated(self):
        sheet,key=make_review(self.snapshot(),'a')
        self.assertEqual(1,len(sheet['items']))
        self.assertEqual(['1','3'],key['item-001'])
        text=json.dumps(sheet)
        for hidden in ('foreign','ScoreBreakdown','Rank','Feedback','ProfileId','secret'):
            self.assertNotIn(hidden,text)
        item=sheet['items'][0]
        self.assertIsNone(item['enjoyment1to5'])
        self.assertIsNone(item['potentialFavorite'])
        self.assertIsNone(item['alreadyKnown'])

    def test_foreign_or_empty_profile_is_rejected(self):
        with self.assertRaises(ValueError): make_review(self.snapshot(),'unknown')
        s=self.snapshot(); s['picks']=[]
        with self.assertRaises(ValueError): make_review(s,'a')

    def test_private_outputs_are_non_overwriting(self):
        with tempfile.TemporaryDirectory() as d:
            p=Path(d)/'baseline.json'
            digest=write_private(p,{'fixture':True})
            self.assertEqual(64,len(digest))
            self.assertEqual(0o600, p.stat().st_mode & 0o777)
            with self.assertRaises(FileExistsError): write_private(p,{'fixture':False})
            self.assertTrue(json.loads(p.read_text())['fixture'])

if __name__=='__main__': unittest.main()
