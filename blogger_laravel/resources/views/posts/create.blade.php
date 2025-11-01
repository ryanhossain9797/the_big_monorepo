{{--                                                  VIEW TO CREATE A NEW POST --}}

@extends('layouts.app')

@section('content')
    <div class="card card-default">
        <div class="card-header">
            {{isset($post)? 'Edit Post':'Create Post'}}
        </div>
        <div class="card-body">
        @if($errors->any())
        <div class="alert alert-danger">
            <ul class="list-group">
                @foreach($errors->all() as $error)
                <li class="list-group-item text-danger">
                    {{$error}}
                </li>
                @endforeach
            </ul>
        </div>
        @endif
        <form action="{{isset($post)?route('posts.update', $post->id):route('posts.store')}}" method="POST" enctype="multipart/form-data">
                @if(isset($post))
                    @method('PUT')
                @endif
                @csrf
                <div class="form-group">
                    <label for="category_id">Category</label>
                    <select name="category_id" id="category_id" class="form-control">
                        @foreach($categories as $category)
                        <option value="{{$category->id}}"
                            @if(isset($post))
                                {{$post->category_id == $category->id? 'selected':''}}
                            @endif
                            >{{$category->name}}</option>
                        @endforeach
                    </select>
                </div>
                <div class="form-group">
                    <label for="title">Post title</label>
                    <input type="text" id="title" class="form-control" name="title" value="{{isset($post)? $post->title:''}}">
                </div>
                <div class="form-group">
                    <label for="description">Description</label>
                    <input type="text" id="description" class="form-control" name="description" value="{{isset($post)? $post->description:''}}">
                </div>
                <div class="form-group">
                    <label for="content">Content</label>
                    <input id="content" type="hidden" name="content" value="{{isset($post)? $post->content:''}}">
                    <trix-editor input="content"></trix-editor>
                </div>
                <div class="form-group">
                    <label for="submitted_at">Published At</label>
                    <input type="text" id="submitted_at" class="form-control" name="submitted_at" value="{{isset($post)? $post->submitted_at:''}}">
                </div>
                @if(isset($post))
                <div class="form-gorup">
                    <label for="current-image">Current Image</label>
                    <img src="{{asset('storage/'.$post->image)}}"  id="current-image" width="100%" alt="">
                </div>
                @endif
                <div class="form-group mt-2">
                    <label for="image">Image</label>
                    <input type="file" id="image" class="form-control-file" name="image" value="{{isset($post)? $post->image:''}}">
                </div>
                <div class="form-group">
                    <button class="btn btn-success">{{isset($post)? 'Update':'Create'}}</button>
                </div>
            </form>
        </div>
    </div>
@endsection

@section('css')
<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/trix/1.2.1/trix.css">
<link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/flatpickr/dist/flatpickr.min.css">
@endsection

@section('scripts')
<script src="https://cdnjs.cloudflare.com/ajax/libs/trix/1.2.1/trix.js"></script>
<script src="https://cdn.jsdelivr.net/npm/flatpickr"></script>
<script>
    flatpickr('#submitted_at', {
        enableTime: true
    })
</script>
@endsection
