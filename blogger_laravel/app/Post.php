<?php

namespace App;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Support\Facades\Storage;
use App\Category;

class Post extends Model
{
    use SoftDeletes;
    protected $fillable = [
        'title' , 'description', 'content', 'image', 'submitted_at', 'category_id'
    ];

    /*
    * Delete Post's image from storage
    *
    * @return void
    */
    public function deleteImage(){
        Storage::delete($this->image);
    }

    public function category(){
        return $this->belongsTo(Category::class);
    }
}
